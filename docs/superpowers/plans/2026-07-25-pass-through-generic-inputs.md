# pass-through 項目の汎用入力化（実装計画）

- 作成: 2026-07-25
- 関連: ADR 0030（帳票入力UIの責務）/ ADR 0032（契約情報の個別入力）/ ADR 0036（グループB個別入力）/ ADR 0039〜0041（版）
- 起点: 「新しい施行分で項目が増えるたびに Domain＋EF＋migration＋snapshot＋DTO＋UI＋readiness の 9 ファイルを触る」問題

> 進捗はこのファイルのチェックボックスが単一の正本。

## 事前調査で確定した事実（計画の前提）

`field-mapping-r7-10.json` の `status: "missing"` は **32 件・24 個の target path**。参照経路を追って
「算定に効くか」で分類した（`ClaimCalculator` / `ClaimCalculationRequestBuilder` / `ClaimAdditionDailyCounts`
への到達で判定。`ClaimInputPolicy` の参照は append-only 検証であって算定ではない）。

| 区分 | 件数 | 内訳 |
|---|---|---|
| **算定に効く（型付き維持）** | 13 | `ClaimInput.UpperLimitManagementResult` / `UpperLimitManagedAmountYen`（上限額管理→負担額）、`DailyRecord` の 8 項目（開始/終了時刻・施設外支援・医療連携・体験利用・地域連携・集中支援・緊急受入 → `ClaimAdditionDailyCounts` 経由で加算回数になる）、`Certificate` の 3 項目（請求先・上限額管理の突合） |
| **転記のみ（汎用化候補）** | 11 | `ClaimInput.ExceptionalUsage{StartMonth,EndMonth,Days}` / `StandardUsageDayTotal` / `MunicipalSubsidyAmountYen` / `SpecialVisitSupportBilledCount` / `OffsiteSupportCumulativeDays`、`ContractedProvider.CertificateEntryNumber` / `FirstServiceDate`、`DailyRecord.SpecialVisitSupportBilledHours`（日次）、`IntensiveSupportEpisode.StartDate` |

**重要**: 転記のみの 11 件は**すべて既に型付き列として実装済み**。したがって ④ は
「今ある項目の移行」ではなく「**今後の施行分で増える項目の受け皿**」を作る作業である。
既存 11 件の移行は行わない（snapshot・readiness・UI・golden に配線済みで、移行は
利用者に見える利益ゼロ・報酬算定への回帰リスクありのため）。

## 対案との比較（着手前に決めること）

| 案 | 新規 pass-through 項目 1 件あたりの作業 | 型安全性 | 実装コスト |
|---|---|---|---|
| **④ 汎用入力化** | spec JSON への宣言のみ（UI は spec 駆動で生成） | 下がる（値は文字列＋spec 駆動検証） | 大（storage＋snapshot＋CSV解決＋readiness＋spec駆動UI） |
| **対案: 型付き列の scaffold** | スクリプトが 9 ファイルの差分と migration を生成し、人がレビュー | 維持 | 小〜中（生成スクリプトとテンプレート） |
| 現状維持 | 手作業 9 ファイル＋migration | 維持 | 0 |

グループBの実測（3 項目）は **20 ファイル・migration 1 本**だった。施行分の更新頻度は
「令和8年6月施行分は事業所編・共通編を改訂していない」（ADR 0039）ことからも分かるとおり、
**毎回項目が増えるわけではない**。

## 範囲（この計画で作るもの／作らないもの）

- 作る: **月次スコープ**（事業所×受給者×サービス提供年月）の汎用 pass-through 入力。
- 作らない: **日次スコープ**の汎用入力（日ごとの値。UI が表になり別設計が必要）。
  日次の新規項目は当面型付き列で追加し、必要になったら別計画で拡張する。
- 作らない: 既存 11 項目の移行。

## 設計

### 宣言（spec が正本）

`field-mapping-{version}.json` の `missing` エントリに `storage` を追加する
（既定は `typed`。`generic` は明示宣言のみ）。

```json
{
  "fieldId": "provider:J611:01:0NN",
  "status": "missing",
  "storage": "generic",
  "targetModel": "ClaimGenericInput",
  "targetProperty": "<名前>",
  "uiSurface": "ClaimInputView",
  "genericInput": {
    "label": "訪問支援特別加算（回）（算定回数）",
    "help": "当月に算定した回数の合計を入力してください",
    "dataType": "numeric", "maxBytes": 2
  },
  "migrationRequired": false
}
```

- `storage: "generic"` は **算定に効かないこと**が条件。証跡台帳（ADR 0038）に
  `supports: ["pass-through"]` の claim を要求し、`CsvSpecificationCatalog` が検証する。
- ラベル・補助文・桁数・属性はすべて spec 側に置く（UI にハードコードしない。ハード制約3）。

### 保存（append-only を壊さない）

汎用値は **`ClaimInput` の子** として持つ（`ClaimInputGenericValue`: `ClaimInputId` FK＋`Name`＋`Value`）。
独立した履歴を作らず、訂正は「新しい `ClaimInput` revision に値の集合を作り直す」で表現する。
既存の `ClaimInputPolicy`（Cancel は payload を持てない等）がそのまま効く。

- 値は文字列 1 列。型は spec の `dataType` で入力時に検証する（数値・年月・日付）。
- 検証は Application が行うが、**spec を直接参照しない**（境界ガード）。
  新設ポート `IClaimGenericFieldCatalog`（Application）を Infrastructure.Csv が実装し、
  宣言（名前・dataType・maxBytes・ラベル・要求条件・uiSurface）を供給する。

### 確定 snapshot・PreviewHash

- `ClaimFinalizationClaimInputSnapshot` に `Generic`（名前→値、**名前昇順**で決定論的に）を追加。
- `ClaimRecipientSnapshotWriter.WriteInputSnapshot` にも同じ順序で含める（PreviewHash に効かせる。
  含めないとプレビュー後に書き換えても同じ hash で確定できてしまう）。

### CSV 生成・readiness

- `ClaimCsvModelPath`: `ClaimGenericInput.<名前>` を DTO の汎用辞書から解決する分岐を 1 つ追加
  （個別 path のハードコードは増えない）。
- `ClaimPreparationContextBuilder`: 宣言された汎用項目の値を同じ path キーで供給する
  （値の組み立ては ADR 0041 で 1 か所に集約済みなので、そこに辞書由来の値を足す）。

### UI（spec 駆動）

- `uiSurface` ごとに 1 セクション（例 `ClaimInputView` の「その他の請求項目」）を置き、
  宣言された汎用項目を `ItemsControl` で並べる。エディタは `dataType` で選ぶ
  （numeric → `NumericUpDown`、date/yearMonth → `TextBox`＋既存 converter）。
- ラベル・補助文は catalog から取る。`ViewInputWiringTests` は「宣言された汎用項目が
  すべて画面に現れること」を**動的に**検査する（binding 名の手書き列挙をしない）。

## タスク

### 1. 宣言と検証

- [ ] `CsvFieldMapping` に `storage` と `genericInput`（label / help / dataType / maxBytes）を追加
- [ ] `CsvSpecificationCatalog`: `storage: "generic"` は `targetModel == "ClaimGenericInput"`・
      `genericInput` 必須・`migrationRequired: false`、かつ証跡台帳に `pass-through` claim を要求
- [ ] 証跡台帳の `supports` 語彙に `pass-through` を追加
- [ ] テスト: 宣言不備（`genericInput` 欠落／算定に効く項目に `generic` を付ける）で fail-close

### 2. ポートと catalog

- [ ] Application に `IClaimGenericFieldCatalog`（宣言の一覧・名前で引く・uiSurface で絞る）
- [ ] Infrastructure.Csv に実装（版ごと。ADR 0039 のレジストリから引く）
- [ ] テスト: 未宣言の名前は fail-close／版ごとに宣言集合が変わること

### 3. 保存（Domain＋EF＋migration 1 本）

- [ ] `ClaimInputGenericValue`（`ClaimInputId` FK・`Name`・`Value`）＋ EF 設定＋migration
- [ ] `ClaimInputPolicy`: Cancel revision は汎用値を持てない
- [ ] `SetClaimInputUseCase`: 汎用値の受け取りと **spec 駆動の検証**（dataType・maxBytes・未宣言名の拒否）
- [ ] テスト: 往復・訂正で集合が入れ替わる・Cancel で空・未宣言名と型不一致は検証エラー

### 4. snapshot と PreviewHash

- [ ] `ClaimFinalizationClaimInputSnapshot.Generic` を末尾 optional で追加（writer / reader）
- [ ] `OperationLocalSnapshotReader` が汎用値を焼き込む
- [ ] `ClaimRecipientSnapshotWriter.WriteInputSnapshot` に名前昇順で含める
- [ ] テスト: 旧 snapshot（キー不在）は空として読める／汎用値を変えると PreviewHash が変わる

### 5. CSV 生成と readiness

- [ ] `ClaimCsvRecipientDto` に汎用辞書を追加し `ExportClaimCsvUseCase` が写像
- [ ] `ClaimCsvModelPath` に `ClaimGenericInput.*` の解決を追加
- [ ] `ClaimPreparationContextBuilder` が宣言された汎用項目の値を供給（ADR 0041 の集約点へ）
- [ ] テスト: 宣言だけで CSV へ出ること／未入力は fail-close されること（要件宣言時）

### 6. spec 駆動 UI

- [ ] `ClaimInputViewModel` に汎用項目の編集モデル（名前・ラベル・dataType・値）
- [ ] `ClaimInputView` に「その他の請求項目」セクション（`ItemsControl`＋dataType 別エディタ）
- [ ] `ViewInputWiringTests` を動的検査へ（宣言された汎用項目が画面に現れることを catalog から検証）
- [ ] アクセシビリティ: 既存欄と同じコントロール種別・間隔・キーボード到達性

### 7. 実証と仕上げ

- [ ] **実証**: 架空の 1 項目を `generic` として宣言し、**C# を 1 行も書かずに**
      入力→確定→CSV 出力まで通ることをテストで示す（この計画の受け入れ基準）
- [ ] `dotnet format` / `./build/ci.sh` 緑
- [ ] ADR 0042（汎用入力の境界と、型付きを既定にする理由）
- [ ] `docs/open-questions.md` 更新／コミット

## リスクと歯

| リスク | 対策（歯） |
|---|---|
| 汎用側が「置き場所として楽だから」既定になる | `storage: "generic"` は spec の明示宣言＋証跡台帳の `pass-through` claim が必須。既定は `typed`。テストで固定 |
| 算定に効く項目が汎用側へ流れる | 宣言時に検証（`targetModel == "ClaimGenericInput"` の値は算定入力の組み立てに一切渡さない構造にする）＋ claim 必須 |
| 型が緩む（文字列 1 列） | 入力時に spec の dataType/maxBytes で検証し、CSV 出力時にも encoder が再検証（二重） |
| PreviewHash に入らず確定後に書き換えられる | writer に名前昇順で含める＋hash が変わることをテストで固定 |
| 日次項目を無理に月次スコープへ入れる | 範囲外と明記。日次は型付きのまま |

## 未確定（着手前に決めたいこと）

1. **④ を実装するか、対案（型付き列の scaffold）にするか。** 転記のみ 11 件はすべて実装済みで、
   ④ の利益は将来の施行分に限られる。
2. 汎用値の**表示名の言語**（spec のラベルは日本語固定でよいか）。
3. 既存 11 件は移行しない方針でよいか（この計画はそう置いている）。
