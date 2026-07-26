# Phase 3-5 設計spec — 指定障害者支援施設区分の構造化入力と請求への結線

> **Status**: 設計合意済（2026-07-26）／未着手
> **位置づけ**: Phase 3-4（R8-06 制度実値投入）が残した「エラーを出さずに誤った請求が生成される」経路のうち、**指定障害者支援施設 variant** を閉じる単一スライス。
> **前提文書**: ADR 0021（体制届の公式コードと構造化入力の要求）／ADR 0025（割合加算の丸め）／ADR 0045（R8 処遇改善の実値・施設別立て率の抽出結果）／ADR 0026・0029・0032・0034（確定 snapshot）

---

## 1. 結論

指定障害者支援施設か否かを `OfficeClaimProfile` の構造化入力として持たせ、入力 UI を用意し、処遇改善加算の施設 variant 4行を投入して請求へ結線する。

これにより Phase 3-4 が残した次の欠陥が閉じる。

> 指定障害者支援施設が体制届 option 2 を届け出た 2026-06 の請求は、**465120 @ 0.105 で成立してしまう**（正しくは 465138 @ 0.116）。施設か否かを判別する構造化入力自体が存在しないため、利用者側に区別する手段がない。**誤った値での過少請求であり、エラーは出ない。**（ADR 0045「確定できなかった区分」表）

到達点は **「施設区分を入力すれば正しい加算行が選ばれ、未入力のまま施設 variant を持つ区分を算定しようとすると明示的に止まる」**。

---

## 2. 背景 — 現状の確認結果（2026-07-26 時点）

### 2.1 器はほぼ揃っている

| 要素 | 状態 |
| --- | --- |
| `ClaimConditionKind.FacilityClassification = 10` | **定義済み**（`ClaimCalculationMasters.cs:74`） |
| schema の `conditionDefinition.kind` に `facility-classification` | **定義済み** |
| `ClaimMasterFileValidator` のトークン種別判定 | **対応済み**（`:546` で parse、`:579` で `isToken` 群に含む） |
| `ServiceCodeResolver` の評価ケース | **無し** → `_ => throw ServiceCodeResolutionException(ConditionUnresolved)` |
| `facility-classification` の `conditionDefinitions` seed | **0件** |
| 施設 variant の `additions` / `service-codes` 行 | **0件** |
| `OfficeClaimProfile` の施設区分フィールド | **無し** |

**resolver が未対応の kind を fail-close することは重要である。** 仮に条件だけを先に seed しても「黙って誤る」のではなく止まる。ただし Phase 3-4 で確認したとおり、`ValidateConditions` の未参照ガードにより条件だけの投入はそもそもできない（§5.4 の投入順で解決する）。

### 2.2 置き場所は既に決定済み

ADR 0021 が記録している（出典 `r8-capability-202606` 372行）。

> 障害者支援施設か否かは `designated-management` から推測せず、**構造化入力を使う**。

ADR 0045 も「`OfficeClaimProfile` 側に『指定障害者支援施設か』の構造化入力が必要（ADR 0021 が既に要求）」と書いている。**本 spec は置き場所を新たに決めるのではなく、既存の決定を実装する。**

### 2.3 値は抽出済み

ADR 0045 が `r8-fee-notice` 物理57頁を `pdftotext -layout` / `-raw` の2方式で抽出し、両方式一致を確認済み。さらに同頁の「改正前」欄（R6 の 93・91・76・62／施設 104・86・69）が既存 seed の `percentage` と完全一致することで**頁と項番の特定が正しいことを二重に確認**している。

下表の「体制届 option」は seed の `conditionDefinitions`（`capability-treatment-improvement-r8-*` の `value`）から機械的に確認した実データである（2026-07-26 検証）。**option 番号は区分の並び順と一致しない**ため、実装時に順序から推測してはならない。

| 区分 | 体制届 option | 通常コード | 通常の率 | 施設別立ての率 | 施設 variant のコード | xlsx 行 |
| --- | ---: | --- | ---: | ---: | --- | ---: |
| (Ⅰ)イ | **2** | `465120` | 0.105 | **0.116** | `465138` | 2262 |
| (Ⅰ)ロ | **7** | `465174` | 0.109 | **0.120** | `465176` | 2264 |
| (Ⅱ)イ | **3** | `465121` | 0.103 | **（別立てなし）** | — | — |
| (Ⅱ)ロ | **8** | `465175` | 0.107 | **（別立てなし）** | — | — |
| (Ⅲ) | **4** | `465122` | 0.088 | **0.098** | `465140` | 2270 |
| (Ⅳ) | **5** | `465123` | 0.074 | **0.081** | `465141` | 2272 |

したがって **施設 variant を持つのは option 2・7・4・5**、**持たないのは option 3・8**である。

(Ⅱ)イ・(Ⅱ)ロ に施設別立てが無いことは、率表の記載と、**xlsx 行 2266・2268 が列A/B/C 空欄でサービスコードを持たない書式上のプレースホルダ行である**ことの両方で確認済み（ADR 0045 の最終レビュー M-1 追記）。

**本スライスで新たに一次資料から値を抽出する必要はない。** 確認するのは xlsx 行 2262/2264/2270/2272 のコードだけである（§7 の2形式独立照合）。

### 2.4 結線経路は既存の構造化入力と同一

```
OfficeClaimProfile.{CapacityHeadcount, StaffingKey, RegionKey}
  → OfficeClaimBillingTokenProvider
  → ClaimBillingConditionTokens
  → ClaimCalculationRequestBuilder（写像できない入力は readiness issue へ）
  → ClaimBillingConditionContext
  → ServiceCodeResolver.Evaluate
```

施設区分はこの経路に1本足すだけで、新しい仕組みは要らない。

---

## 3. スコープ

### 3.1 データモデル

**`FacilityClassification` enum を Domain に追加する。** `R8ReformStatus` と同型にする。

```csharp
public enum FacilityClassification
{
    Unknown = 0,
    General = 1,                    // 指定障害者支援施設以外
    DesignatedSupportFacility = 2,  // 指定障害者支援施設
}
```

**`OfficeClaimProfile.FacilityClassification`** を `FacilityClassification?` として追加する（`ReformStatus` が `R8ReformStatus?` であるのと同じ形）。migration 名は `Phase35OfficeFacilityClassification`。

`OfficeClaimProfileConfiguration` の `CK_OfficeClaimProfiles_CancelPayload` チェック制約に新列を追加する（Cancel レコードは全ペイロード列が NULL であることを要求する既存の不変条件）。

### 3.2 トークンと context

**`ClaimBillingConditionTokens.FacilityClassification`** を `string?` として追加する（`StaffingKey` と同じ）。`OfficeClaimBillingTokenProvider` が enum → トークン文字列へ写す。

**`ClaimBillingConditionContext.FacilityClassification`** を **`string?`（nullable）** として追加する。

**nullable にすることが本設計の要点である。** 既存の `OfficeCapabilityKeys` が同じ設計を採っており、その doc コメントが理由を述べている。

> 集合が未取得（null）の場合は判定不能としてフェイルクローズする（推測しない）。

これにより resolver は **施設条件を持つ行を評価しようとしたときだけ**止まる。(Ⅱ)イ・(Ⅱ)ロ しか算定しない事業所や、処遇改善加算を算定しない事業所は未入力でも影響を受けない。「fail-close で明示入力を強制する」という方針が、必要な範囲に限定された形で実現する。

### 3.3 resolver

`ServiceCodeResolver.Evaluate` の switch に1ケース追加する。

```csharp
ClaimConditionKind.FacilityClassification => EvaluateFacilityClassification(definition, context),
```

`EvaluateFacilityClassification` は `context.FacilityClassification` が `null` のとき `ServiceCodeResolutionException(ConditionUnresolved)` を投げ、非 null のときは既存の `EvaluateToken` と同じ比較を行う。`EvaluateCapability` が null 時に fail-close するのと同じ作法にする。

### 3.4 seed

| 対象 | 変更 |
| --- | ---: |
| `service-codes.json` の `conditionDefinitions` | `facility-classification` を **2件**追加（施設／非施設。`effectiveFrom: "2026-06"`） |
| `additions.json` | 施設 variant **4行**追加（率 0.116 / 0.120 / 0.098 / 0.081） |
| `service-codes.json` の `entries` | 施設 variant **4行**追加（コード 465138 / 465176 / 465140 / 465141） |
| 既存の (Ⅰ)イ・(Ⅰ)ロ・(Ⅲ)・(Ⅳ) の `service-codes` 行 | `conditionSelectors` に**非施設条件を追加** |
| 既存の (Ⅱ)イ・(Ⅱ)ロ の行 | **変更しない** |

**なぜ通常行にも条件が要るか。** R8 改定対象の12区分では option code が R6 と重ならないため、R6 行に `r8-reform-status` 条件を付けなくても曖昧にならなかった。しかし施設 variant は**同じ option code を共有する**（施設の (Ⅰ)イ も体制届 option 2）。片側だけに条件を付けると、施設事業所で通常行（条件なし＝無条件一致）と施設行の**2行が一致し `AmbiguousMatch`** になる。両側に付けて初めて一意に決まる。

**(Ⅱ)イ・(Ⅱ)ロ に条件を付けてはならない。** 施設 variant が存在しないため、条件を付けると施設事業所が (Ⅱ) を算定できなくなる。この非対称は公式の構造がそうなっていることに由来する（§2.3）。

### 3.5 既存行の `conditionSelectors` を変更することについて

append-only の原則に対する例外に見えるが、次の理由で安全である。**この判断を ADR 0047 に明記する。**

1. **確定済みの請求は snapshot から読むため遡って変わらない。** CSV も3帳票も確定 snapshot だけを読む（ADR 0026・0029・0032・0034）。マスタ行の再解決は起きない。
2. **条件の追加は適用範囲を狭める方向**であり、新しい行が選ばれるようになるのではなく、選ばれる条件が厳密になる。
3. 対象行は Phase 3-4 で 2026-06 施行分として追加したばかりで、**本番で 2026-06 の請求が確定された実績はまだ無い**。

ただし **2026-06 以降の請求を既に確定済みの環境があれば、その月のプレビューは変わりうる**（確定済みデータは変わらない）。この注意を受け入れ証跡に残す。

### 3.6 入力 UI

`ClaimInputView.axaml` の構造化入力群（`CapacityHeadcount` / `StaffingKey` / `RegionKey`、148〜159行付近）に **ComboBox を1つ追加**する。enum を選択肢にするため、既存の `ReformStatusOptions` ＋ `ReformStatus` の ComboBox とまったく同じ作法にする。

- `ClaimInputViewModel` に `FacilityClassificationOptions`（`ReformStatusOptions` と同型）と `FacilityClassification` プロパティを追加
- 保存経路（`:452-454` の `CapacityHeadcount` / `StaffingKey` / `RegionKey` と同じ場所）、読込経路（`:817-819`）、クリア経路（`:1036-1038`）の3か所に配線
- ラベルは「施設区分」

**新しい View は作らない。** 施設区分は既存の請求プロファイル入力の1項目であり、専用画面を作る理由がない。

### 3.7 readiness

`ClaimCalculationRequestBuilder` に、施設区分が未入力のときの通知を追加する。**`IsReady` を落とさない警告**とする（ADR 0041 が確立した「確定は止めないが不足を知らせる」形）。

理由: 処遇改善加算を算定しない事業所や (Ⅱ) のみの事業所にとって施設区分は不要であり、必須にすると不要な入力を強いる。実際に必要な場面（施設 variant を持つ区分の算定）では resolver が明示的に止めるので、見落としは起きない。

---

## 4. 非スコープ

| 項目 | 理由 |
| --- | --- |
| 施設での体制届 option 集合の絞り込み（ADR 0021: R8-06 の `treatment-improvement` は `{1,2,4,5,7}`） | 率とは別の入力バリデーションの話。一次資料の再確認と体制届側の変更を伴うため別スライス |
| 処遇改善(Ⅴ) 14区分の経過措置 | ADR 0045 が未投入としたもう一方のギャップ。区分数・率対応が複雑で独立したスライス |
| R6 期の施設 variant（`466774` 等） | 本スライスは R8-06 に限定する。R6 期の請求で施設 variant が必要になった時点で別途 |
| 処遇改善以外の施設別立て加算 | 一次資料で存在を確認していない。必要が判明した時点で別途 |
| `Office` エンティティへの施設フラグ追加 | ADR 0021 が `OfficeClaimProfile` の構造化入力と決めている。二重の真実を作らない |

---

## 5. エラー処理の方針

- **施設区分が未入力のまま施設 variant を持つ区分を算定しようとしたら止める。** `ConditionUnresolved` で fail-close する。推測して通常行を選ばない。
- **一次資料からコードを一意に確定できない行は seed しない。** 4行のうち確定できないものがあれば、その行だけ投入せず `docs/open-questions.md` へ起票する。部分完了を許容する。
- **`AmbiguousMatch` が出たら設計の誤りである。** 通常行と施設行の両方が一致する状態は、条件の付け方を誤った証拠。テストで多重一致0を機械検証する（§6）。

---

## 6. テスト戦略

TDD（Red → Green → Refactor）。各段で「実装したデータそのものを消す/変えると RED になるか」で歯を確認する。

| 層 | テスト | 内容 |
| --- | --- | --- |
| Domain | `ServiceCodeResolverTests`（拡張） | `FacilityClassification` 条件の評価。null で `ConditionUnresolved`、一致/不一致 |
| Domain | `ClaimCalculatorGoldenCaseTests`（拡張） | 施設 × (Ⅰ)イ × 2026-06 の worked example。期待値の出典は ADR 0047 の決定表 |
| Infrastructure | `ClaimMasterR8BoundaryTests`（拡張） | 施設／非施設 × **option 2・7・4・5**（施設 variant を持つ4区分）の解決が一意で、施設側が施設コードへ解決すること。**施設 × option 3・8（(Ⅱ)イ・(Ⅱ)ロ）が通常行に解決すること** |
| Infrastructure | 新規（曖昧性検査） | 施設区分2値 × 体制届 option 全値の組合せで**多重一致0件**を機械検証 |
| Infrastructure | `ClaimAdditionSeedScopeTests`（拡張） | 2026-06 の処遇改善コード集合が10件（通常6＋施設4）で**上限も固定** |
| Infrastructure | 新規（率の pin） | 施設 variant 4件の率を production seed から数値 pin（ADR 0047 の決定表が出典） |
| Application | `ClaimCalculationRequestBuilderTests`（拡張） | 施設区分未入力時に警告が出て `IsReady` は落ちないこと |
| App | `ClaimInputViewModelTests` / `ViewInputWiringTests`（拡張） | ComboBox の配線、保存・読込・クリアの往復 |
| Infrastructure | `Phase35...MigrationTests`（新規） | 新列の追加とラウンドトリップ |

**歯の確認（必須）**: (a) 施設 variant の率を1桁変えると率 pin が RED、(b) 通常行から非施設条件を消すと曖昧性検査が RED、(c) (Ⅱ) 行に誤って施設条件を付けると「施設 × option 3」の解決テストが RED。

---

## 7. 一次資料の扱い

新たな値の抽出は不要（§2.3）。確認するのは**サービスコードの実在**だけである。

- `r8-service-codes-2-xlsx`（SHA `307b631ed91a…`）と `r8-service-codes-2-pdf`（SHA `0ff507138037…`）を取得し、`shasum -a 256` で `sources.json` 登録値と照合する
- 行 2262 / 2264 / 2270 / 2272 のコード（465138 / 465176 / 465140 / 465141）を **2形式独立**で突合する
- 率は ADR 0045 の抽出結果を出典とし、ADR 0047 の決定表へ**転記元を明示して**再掲する
- `locator` は位置指定のみ。物理頁と印字頁を区別する

---

## 8. ADR 計画

**ADR 0047**（着手時に空き番号を再確認）。構成は `結論 → 背景 → 選択肢 → 決定 → 影響`、**初手から確定**として書く。

必須の記載事項:

- 施設 variant 4行の決定表（区分・コード・率・xlsx 行・体制届 option）
- (Ⅱ)イ・(Ⅱ)ロ に施設別立てが無いことと、その一次資料上の根拠
- **通常行にも非施設条件を付ける理由**（同一 option code の共有による多重一致）と、(Ⅱ) を対象外にする理由
- **既存行の `conditionSelectors` を変更する判断**と、確定 snapshot により遡及しないという根拠（§3.5）
- 選択肢: 「施設行にだけ条件を付ける」を**多重一致になるため不採用**として記録する。「resolver に優先順位を導入する」も**十分にテストされた解決器の意味論を変えるため不採用**として記録する
- 未入力時の挙動（`ConditionUnresolved` で fail-close、readiness は警告）
- ADR 0045「確定できなかった区分」表の施設 variant 行を本 ADR が引き取ったこと

---

## 9. 成果物

- `src/Tsumugi.Domain/` — `FacilityClassification` enum、`ClaimBillingConditionContext` 拡張、`ServiceCodeResolver` の評価ケース
- `src/Tsumugi.Domain/Entities/OfficeClaimProfile.cs` — 新プロパティ
- `src/Tsumugi.Application/` — `ClaimBillingConditionTokens` 拡張、`ClaimCalculationRequestBuilder` の readiness、保存ユースケースの配線
- `src/Tsumugi.Infrastructure/` — migration `Phase35OfficeFacilityClassification`、`OfficeClaimProfileConfiguration`、`OfficeClaimBillingTokenProvider`、seed 3種の変更
- `src/Tsumugi.App/` — `ClaimInputViewModel` / `ClaimInputView` の ComboBox
- `docs/decisions/0047-…md`
- テスト（§6）
- `docs/open-questions.md` — 施設 variant 未投入の項目をクローズ（処遇改善(Ⅴ)と体制届 option 絞り込みは残す）
- `docs/phase3-5-acceptance.md` / `CHANGELOG.md` / `CLAUDE.md`「現在地」

---

## 10. リスク

| リスク | 影響 | 対応 |
| --- | --- | --- |
| **通常行への条件追加を忘れる** | 施設事業所で `AmbiguousMatch`。請求が止まる（誤請求にはならない） | §6 の曖昧性検査で機械検証。全組合せで多重一致0を要求 |
| **(Ⅱ) 行に誤って条件を付ける** | 施設事業所が (Ⅱ) を算定できなくなる（無音の未算定） | 「施設 × option 3・5 が通常行に解決する」テストで固定 |
| 2026-06 以降を既に確定済みの環境がある | その月のプレビューが変わりうる（確定済みデータは不変） | 受け入れ証跡に注意として記録 |
| xlsx の行番号が ADR 0045 の記載とずれている | 誤ったコードを投入 | 2形式独立照合。ずれたら seed せず起票 |
| readiness を必須にしてしまう | 処遇改善を算定しない事業所に不要な入力を強いる | 警告に留める設計を §3.7 で固定。テストで `IsReady` が落ちないことを確認 |

---

## 11. 未確定事項（着手時に確認する）

- 施設／非施設のトークン命名。既存の `r8-reform-status-target` の作法に合わせるが、`facility-classification` kind の seed 前例が無いため本スライスが規約を作る。ADR 0047 に記録する。
- 施設 variant のコードが、通常行と**同じ体制届 option を共有する**こと（例: 施設の (Ⅰ)イ も option 2）。§2.3 の対応表は通常行側を実データで確認したものであり、施設 variant 側の option は ADR 0021 の「令和8年6月追加コード」節と `r8-claim-decision-xls` 5020〜5023行で確認する。**一意に読めない区分は投入しない。**

  もし施設 variant が**別の option を持つ**ことが判明した場合、多重一致は起きないので §3.4 の「通常行にも非施設条件を付ける」設計は不要になる。その場合は設計へ差し戻し、条件の付け方を再判断する。

---

## 12. 参照

- `docs/decisions/0021-office-capability-official-codes.md` — 構造化入力の要求（239行）、R8 追加コード一覧（185行付近）
- `docs/decisions/0045-r8-treatment-improvement-addition-values.md` — 施設別立て率の抽出結果、コードと xlsx 行、行2266・2268 の確認、「確定できなかった区分」表
- `docs/decisions/0025-claim-rounding-rules.md` — 割合加算の丸め
- `docs/phase3-4-acceptance.md` — 本スライスが引き取る残課題の記録
- `docs/superpowers/specs/2026-07-26-phase3-4-r8-06-master-values-design.md` — 直前のスライス
