# 0042 転記専用の請求入力は spec 宣言だけで増やせるようにする

- 状態: 採用（2026-07-25）
- 関連: ADR 0030（帳票入力UIの責務）/ ADR 0032・0036（個別入力）/ ADR 0038（行単位の出典）/ ADR 0039〜0041（版）
- 計画: `docs/superpowers/plans/2026-07-25-pass-through-generic-inputs.md`

## 結論

**算定に効かない転記専用の項目**は、CSV 仕様に `storage: "generic"` と宣言するだけで
「入力欄が出る・readiness で要求される・CSV へ出る」状態になる。Domain の型付き列・migration・
DTO・snapshot・UI の追加は不要。

**算定に効く項目は型付きのまま**（既定は `typed`）。汎用側は明示宣言＋証跡が必要で、既定にはならない。

## 事前調査（判断の根拠）

`missing` 32 件・24 target path を「算定に効くか」で分類した（`ClaimCalculator` /
`ClaimCalculationRequestBuilder` / `ClaimAdditionDailyCounts` への到達で判定。`ClaimInputPolicy` の
参照は append-only 検証であって算定ではない）。

- **算定に効く 13 件**: 上限額管理 2 件、`DailyRecord` 8 件（加算回数になる）、`Certificate` 3 件。
- **転記のみ 11 件**: 例外利用日 4 件、自治体助成額、算定回数、施設外支援累計、事業者記入欄番号、
  初回サービス提供日、算定時間数（日次）、集中支援開始日。

転記のみの 11 件は**すべて既に型付き列として実装済み**。したがって本 ADR は
「今後の施行分で増える項目の受け皿」であり、**既存 11 件は移行しない**
（配線済みで、移行は利用者に見える利益ゼロ・報酬算定への回帰リスクありのため）。

## 決定

1. **宣言は spec が正本**。`field-mapping-{version}.json` の `missing` に `storage: "generic"` と
   `genericInput`（label / help / dataType / maxBytes）を置く。`dataType` と `maxBytes` は
   項目定義と一致することを読み込み時に検証する。ラベル・補助文も spec 側に置き、UI に書かない。
2. **`generic` は「算定に効かない」ことの証跡を要求する**。証跡台帳（ADR 0038）に
   `supports: ["pass-through"]` の claim が無ければ**仕様の読み込みが失敗する**。
   これが「楽だから汎用側へ流す」ことへの歯。
3. **保存は `ClaimInput` の子**（`ClaimInputGenericValue`）。独立した履歴を持たず、訂正は親の
   新 revision で集合を作り直す。`AppendOnlyGuard` の対象に加え、Cancel revision は値を持てない。
4. **型・桁数の検証は仕様を所有する層**（`IClaimGenericFieldCatalog.ValidateValue`）。
   Application に `dataType` の語彙を持ち込まない（境界ガードが実際に落ちた）。
   出力時は encoder が同じ規則で再検証する（二重の網）。
5. **範囲は月次スコープのみ**。日次スコープ（日ごとの値）は UI が表になり別設計が必要なので、
   日次の新規項目は当面型付き列で追加する。
6. **PreviewHash に含める**（名前昇順）。含めないとプレビュー後に書き換えても同じ hash で確定できる。

## 受け入れ基準（実証済み）

`GenericPassThroughInputTests`（6 件）が、埋め込み catalog の 1 項目の**宣言だけを差し替えた** catalog で
次を示す。C# の変更は 1 行も無い。

- 宣言だけで仕様の検証を通る（`migrationRequired: false`）
- 宣言だけで**入力欄の定義**になる（ラベル・型・桁数・画面が宣言から出る）
- 宣言だけで**readiness の要件**になる
- 宣言だけで**CSV へ出る**（値が該当項目の位置に現れる）
- 未入力なら**出力側で項目 ID 付きに fail-close** する（汎用側だから緩くならない）
- `pass-through` claim が無ければ**読み込みで落ちる**

## 影響

- 追加: `ClaimInputGenericValue`（migration `Phase33ClaimInputGenericValues`）、
  `IClaimGenericFieldCatalog` / `CsvGenericFieldCatalog`、`ClaimGenericInputField`（UI）、
  `ClaimInputView` の「その他の請求項目」セクション（`ItemsControl` で宣言を並べる）。
- readiness の値供給は ADR 0041 の集約点に合流し、**宣言された名前の分だけ**キーを置く
  （キーを置かないと Unresolved になり「入力すれば済む」ことが伝わらない）。
- `ViewInputWiringTests` は汎用項目について binding 名を列挙せず、「宣言を並べる仕組みがある」ことを検査する。
- record 等値がコレクション member を参照比較するため、snapshot 比較のテスト 1 件を構造比較へ変更した。

## 残り

- 日次スコープの汎用入力（別計画）。
- 既存 11 件の移行はしない（この ADR の決定）。
