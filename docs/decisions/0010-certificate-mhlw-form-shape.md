# ADR 0010: 受給者証は MHLW 様式（障害者総合支援法施行規則 第六十五条の七）の主要セクションを保持する

- 結論: `Certificate` エンティティを MHLW 公式様式の以下 5 セクションを保持する形に拡張し、サービス事業者記入欄は別エンティティ `ContractedProvider` として 1 対多で正規化する。
  1. 支給決定障害者等情報（居住地・性別・保護者氏名/続柄を発行時点のスナップショットとして保持）
  2. 障害種別（身体/知的/精神/難病等の 4 フラグ＋障害支援区分 1〜6 / None）
  3. 給付種別と支給決定内容（介護給付/訓練等給付/障害児通所、サービス種別、有効期間、支給量、月額上限、特記事項）
  4. 計画相談支援給付費の支援内容（事業者名、事業所番号、支援期間）
  5. 利用者負担に関する事項（負担区分、上限額管理事業所、食事提供体制加算、高額障害福祉サービス費等の適用）
  6. サービス事業者記入欄（`ContractedProvider` テーブル、1 受給者証 ⇔ N 事業所契約）
- 背景:
  - フェーズ1初期実装の `Certificate` は受給者証番号・有効期間・支給量・月額上限・市町村の 5 項目のみで、実運用で必要な様式項目を満たしていなかった。
  - 受給者証は様式が告示で定義された公文書であり、`docs/open-questions.md` に挙げた性別等の必須項目を Phase 3 まで先送りすると、現場の入力業務に支障が出る。
  - CLAUDE.md §ハード制約 3「公式資料から一意に確定できない値は推測で埋めない」に従い、MHLW 公式様式に**直接対応する項目のみ**を採用し、自治体独自項目や金額算定値はここでは扱わない。
- 選択肢:
  - (a) **採用**: 主要セクションを Certificate 本体＋ContractedProvider 子テーブルにフラット展開。enum は MHLW 用語の固定値（区分・給付種別・負担区分）に限定し、その他は自由記述（string）とする。
  - (b) Certificate を変更せず、別エンティティ `CertificateDetail` に拡張項目を寄せる。リレーション 1:1 だが I/O が常に二重になり、`AppendOnlyGuard` の append-only セマンティクスとの整合が複雑化する。
  - (c) JSON 1 列に全部入れる。型安全性・索引性・将来のCSV出力との突合性が損なわれる。
- 決定: (a)。理由:
  - Certificate は append-only （`AppendOnlyGuard` で UPDATE 禁止）のため、列追加は段階的に行ってもデータ破壊が無い（既存行は新列が NULL/既定値で残る）。
  - サービス事業者記入欄のみ 1:N 構造なので別テーブル化が自然。`ContractedProvider` は契約終了日が後日設定されうるため append-only 対象から外す（master 型に分類）。
  - enum は MHLW 用語の固定値で OK、自治体差は文字列で許容する（`Municipality` の運用と同じ）。
- 影響:
  - DB マイグレーション `20260627220601_ExpandCertificateAndContractedProvider` を生成。既存の Certificate 行には NULL/既定値で新列が追加される（オフライン専用アプリでもデータ破壊なし）。
  - `RegisterCertificateUseCase` は `RegisterCertificateInput` レコードを引数に取り、必須最小入力＋任意セクション項目の組合せを表現する。互換性なし破壊的変更（テストとアプリ層を同時更新済み）。
  - `RegisterContractedProviderUseCase` / `ListContractedProvidersUseCase` / `ListCertificatesByRecipientUseCase` / `IContractedProviderRepository` を新設。
  - 受給者証 View は 6 セクションのフォームに再構築（ComboBox/CheckBox/TextBox の組合せ）。
  - 自治体独自項目や、金額算定（食事提供体制加算の単位数等）の確定値は Phase 3 報酬告示と突合して別途追加する。
- 関連: CLAUDE.md §ハード制約 3、`docs/open-questions.md`（性別など利用者属性の拡張・自治体差の項）、`src/Tsumugi.Domain/Entities/Certificate.cs`、`src/Tsumugi.Domain/Entities/ContractedProvider.cs`、`src/Tsumugi.Application/UseCases/Certificate/RegisterCertificateUseCase.cs`。
