# ADR 0053: Contract と ContractedProvider の責務分離

- 状態: 確定（2026-08-17）
- 関連: [ADR 0032](0032-contract-information-as-individual-input.md)、[ADR 0010](0010-certificate-fields.md)（受給者証入力面）
- 関連 open-question: 「ContractedProvider と Contract の整理」（本 ADR でクローズ）

## 結論

工賃の当月対象選定の正本は **`Contract`**、請求 CSV 契約情報の正本は **自事業所行を含む `ContractedProvider`** とする。[ADR 0032](0032-contract-information-as-individual-input.md) を維持し、自社を `ContractedProvider` から除外する運用は採らない。二重入力 UI の警告と、証訂正後の `ContractedProvider` staleness 自動修復は **S4 の対象外**である。

| 系統 | 正本 | 根拠 |
|---|---|---|
| 工賃の当月対象選定 | `Contract` | Phase 2 既存（利用契約の期間マスタ） |
| 請求 CSV 契約情報（`provider:J121:05` / `J121:02:008`） | `ContractedProvider`（自事業所行必須） | ADR 0032 |

## 背景

Phase 1 時点の open-question は次を未決のまま残していた。

- `Contract` は自社事業所の利用契約のみを表現する。
- `ContractedProvider` は受給者証「サービス事業者記入欄」に書かれる全契約事業所を網羅する。
- 重複格納を避けるため「自社は `Contract` のみ／`ContractedProvider` には他事業所のみ」とする運用が候補だった。

その後 Phase 3-3 で [ADR 0032](0032-contract-information-as-individual-input.md) が確定し、請求 CSV の契約情報は **`ContractedProvider` の個別入力**を正本とし、確定時に snapshot へ焼き込む方式になった。`OperationLocalSnapshotReader` はサービス事業者記入欄から**自事業所（事業所番号一致）の行**を選ぶ。該当行が無ければ CSV 側で fail-close する。

したがって「自社を `ContractedProvider` から除外する」案は、ADR 0032 と両立しない。ロードマップ当初の AC4-8 推奨文（自社は Contract のみ）は、この確定後の事実に合わせて破棄する必要がある。

Phase 4 S4 では UI 補完（精神手帳更新アラート・フェースシート履歴差分・障害種別整合警告）と並び、本責務分離を ADR として文書化した。スキーマ変更・migration・登録フロー変更は行っていない。

## 選択肢

### A: 自社は `Contract` のみ、`ContractedProvider` には他事業所のみ書く（不採用）

ロードマップ当初案。重複入力を減らせる一方、ADR 0032 の「自事業所行を選んで CSV 契約情報にする」前提を壊す。不採用。

### B: `Contract` を廃止し `ContractedProvider` に一本化する（不採用）

工賃の当月対象選定が Phase 2 以降 `Contract` に依存しており、一本化はスキーマと工賃ユースケースの大規模変更になる。S4 の範囲外であり、本 ADR では採らない。

### C: 責務分離を維持し、自事業所も請求用に `ContractedProvider` 行を持つ（採用）

`Contract` と `ContractedProvider` は目的が異なるため並存させる。自事業所行は請求 CSV に必須。二重入力は運用上の注意事項とし、警告 UI と staleness 自動修復は別スライスへ残す。

## 決定

### 決定1: 工賃の正本は `Contract`

工賃計算における当月対象利用者の選定は、既存どおり `Contract`（自社利用契約の期間）を正本とする。本 ADR で工賃側の参照先を変更しない。

### 決定2: 請求 CSV 契約情報の正本は自事業所を含む `ContractedProvider`

[ADR 0032](0032-contract-information-as-individual-input.md) を維持する。契約支給量・契約開始／終了年月日・事業者記入欄番号・初回サービス提供日は `ContractedProvider` の個別入力を正本とし、確定 snapshot 経由でのみ CSV が読む。自事業所行が無い月は CSV 生成が fail-close する。

### 決定3: 二重入力警告 UI と staleness 自動修復は S4 外

`Contract` と自事業所の `ContractedProvider` は内容が重なりうるが、S4 では警告バナーや保存時検証を追加しない。受給者証訂正後に `ContractedProvider.CertificateId` が古くなる staleness の自動修復も対象外とする。いずれも未解決のまま残す。

## 影響

### 運用への帰結

- 請求を出す月では、受給者証のサービス事業者記入欄に**自事業所の行**を入力する必要がある（ADR 0032 と同旨）。
- 工賃対象の選定は従来どおり `Contract` で行う。請求用入力を省略しても工賃計算自体は動くが、CSV は出ない。

### 残る限界

1. **二重入力は運用で吸収する。** `Contract` と自事業所 `ContractedProvider` の期間・支給量が食い違っても、本 ADR 時点では機械警告しない。
2. **証訂正後の `ContractedProvider` staleness は自動修復しない。** 訂正後に記入欄を手で直す必要がある場合がある。
3. **オフライン責務・伝送範囲は変えない。** 本 ADR は入力の正本の整理のみであり、国保連への伝送・電子証明書処理は引き続きアプリの範囲外である。

### テスト／実装との関係

S4 の実装は Domain の純粋関数・Query UseCase・既存タブへの読み取りパネルであり、本 ADR の決定をコード変更で強制してはいない。強制は既存の ADR 0032 経路（snapshot 選択・CSV fail-close）が担う。
