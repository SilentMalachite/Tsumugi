# Phase 3-1 再設計: 最小垂直スライスによる請求算定パイプライン

- **Status: Approved（設計承認済み・実装計画は別途 writing-plans で作成）**
- 作成日: 2026-07-19
- 本specはPhase 3-1の**再設計正本**である。置換対象は「§9 置換対象一覧」を参照。
- グローバル制約は `docs/superpowers/specs/2026-06-29-phase3-claim-data-design.md`（Phase 3全体設計）と `06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md`（正本仕様）を引き続き継承する。本specはそれらの範囲内で「進め方と当面のスコープ」を再定義するものであり、正本仕様の受け入れ基準（AC3-1〜）を変更しない。

---

## 1. 再設計の理由（現状診断）

2026-07-19時点の調査で確認した事実:

1. **状態表現の三重分裂**: 現行マスター計画（Task 1〜28、2,054行）は、散文の進捗ノート（2026-07-12で停止・「次=Task 11」のまま）、チェックボックス（Task 11以降 0/129 のまま凍結）、実コード（Task 11入力UI・Task 12 schema v2まで実装済み）の3系統が全て食い違い、現在地が判定不能。
2. **文書の増殖**: Task 12/13領域だけで計画・spec文書が6本以上にspin-offし、旧版と現行版の判別に各文書の冒頭精読が必要な状態。
3. **中核の空洞化**: 入力側（ClaimInput系Entity/Policy/UseCase・入力UI）と確定側（ClaimBatch・ClaimFinalizationOperationV1・migration）の「両端」は実装済みだが、その間を埋める「seed実値 → resolver → 算定 → snapshot → 確定UseCase」が全て未実装。制度マスタseed 6ファイルは全て `"entries": []` の空スタブ。
4. **原則の転化**: 「制度値を推測しない」原則が、本来の運用（open-questionsへ起票して先へ進む）ではなく、保護施設B型公式契約・source inventoryの文書無限リファイン（直近4日119コミット中70%がdocs/fix）へ転化していた。

再設計で直すのは **(1) 空洞の中核パイプライン、(2) タスク・文書構造、(3) 未確定値の扱いルール** の3点である。実装済みコードの品質は本specの問題対象ではない（資産として再利用する）。

---

## 2. ゴール

**典型的な就労継続支援B型事業所の1ヶ月分の請求について、入力 → 算定プレビュー → 確定までがUIから通り、golden caseテストで公式計算例と一致すること。**

- 汎用アプリとして、定員規模・平均工賃月額区分は全区分をカバーする。
- 加算は利用頻度の高い主要なものに限定する。
- 仕様未確定の値はスコープから外すことで停滞を構造的に解消する。

---

## 3. スコープ

### 3.1 含む

1. **基本報酬**: B型の `(対象年月, 報酬体系, 人員配置, 定員規模, 平均工賃月額, 経過措置コンテキスト)` → サービスコード・単位数の解決。定員規模×平均工賃月額区分は**全区分**をseedする。
2. **令和6現行値 + 令和8年6月の区分見直し・経過措置**: `適用開始年月` による切替。境界月（2026-05/2026-06）テストを含む（正本AC3-1。本日時点で令和8年6月改定は既に発効しており、実請求に両バージョンが必要）。
3. **主要加算（典型セット・候補）**: 送迎加算、欠席時対応加算、食事提供体制加算（ADR 0020確定済）、目標工賃達成指導員配置加算、福祉専門職員配置等加算、初期加算、処遇改善加算系。
   **最終リストはseed投入時に「一次資料から単位数・算定条件を一意に確定できたものだけ」をADRで確定する。** 本specの候補列挙を根拠に値を推測してはならない。
4. **地域区分単価・利用者負担**: 総費用額、給付率、証記載上限、上限額管理結果を入力とする基本パス。
5. **確定・再確定・取下げ**: 既存の `ClaimFinalizationOperationV1` / `IClaimFinalizationStore` / `ClaimBatch` 土台への接続。
6. **平均工賃月額の正式式**: `年間工賃支払総額 ÷ (年間延べ利用者数 ÷ 年間開所日数) ÷ 12` をDomain純粋関数として実装（ADR 0023）。Phase 2の `AverageWageMetric` は互換保持。

### 3.2 含まない（凍結・スコープ外）

1. **保護施設事務費・基準該当B型**: schema v2の契約・validator・契約テストは資産としてそのまま残す（削除はしない）。実値seed・入力UI・runtime算定は将来スライスへ凍結。**この領域に関する文書修正・契約リファインも凍結する。**
2. **返戻・過誤調整・再請求の自動ワークフロー**（正本3.2の通り）。
3. **一次資料から確定できない値のservice code**: そのコードごとスコープから外し、`docs/open-questions.md` に1行起票して先へ進む。
4. 帳票（Phase 3-2）・CSV出力（Phase 3-3）は本スライスの対象外（既存の後続フェーズ計画の領域）。

---

## 4. 文書・状態管理ガバナンス

### 4.1 正本の一本化

- 本spec 1本 + これから作成する実装計画1本のみをPhase 3-1の正本とする。
- 旧文書は**一切編集しない**。置換宣言は本specの§9に集約する（superseded ヘッダ行脚はそれ自体がchurnになるため行わない）。
- `CLAUDE.md` の「現在地」を本spec/新計画基準に更新する。

### 4.2 状態表現の一本化

- 進捗は**新実装計画のチェックボックスのみ**で表す。散文の「実装進捗ノート」方式は廃止。
- チェックを入れるコミットには、対応するテスト緑の証跡（実行コマンド）を含める。

### 4.3 文書増殖の禁止ルール

1. タスクのspin-off計画文書を新規作成しない。粒度が大きすぎた場合は**計画内でタスクを分割**する。
2. 仕様疑義は `docs/open-questions.md` に1行起票 → 解決はADR 1本 → 計画には結果だけ反映する。
3. 契約文言のリファインを目的とするdocsコミットを禁止する。
4. **回路ブレーカー**: 計画文書の修正が3コミット連続したら手を止めてユーザーに相談する。

### 4.4 ワークツリーの浄化（実装計画の最初のタスク）

- 現行マスター計画への未コミットdiff（Task 11分割の追記 +236行）は破棄する（本specで置換されるため）。
- `graphify-out/` を `.gitignore` に追加する。
- `.serena/project.yml` は内容確認の上コミットまたは破棄する。
- `docs/superpowers/plans/2026-07-11-phase3-0-task16-acceptance-closeout.md` はコミットしてPhase 3-0を正式クローズする。

---

## 5. アーキテクチャ（中核パイプライン）

新しい抽象は増やさない。**既に定義済みのinterface（`IClaimMasterProvider`、`IClaimSnapshotValidationCodecRegistry`、`IClaimFinalizationStore` 等）の実装を埋める。**

```
[seed JSON 6ファイル] ← 実値投入（典型セット）【新規】
        ↓ JsonClaimMasterProvider（既存） + ClaimMasterFileValidator（既存）
[ClaimCalculationMasters]（既存）
        ↓ ServiceCodeResolver【新規・Domain純粋関数】
        │   (対象年月, 報酬体系, 人員配置, 定員, 平均工賃, 経過措置) → サービスコード + 単位数
        ↓ ClaimCalculator【新規・Domain純粋関数】
        │   入力: ClaimInput / DailyRecord / Certificate / OfficeClaimProfile のsnapshot値
        │   出力: 明細（基本 + 加算減算） → 単位数合計 → ×地域単価 → 総費用額 → 利用者負担
        ↓ CalculateClaimUseCase【新規・Application】
        │   ├ SnapshotReader【新規】: operation-local read txで入力を値として閉じる
        │   └ canonical PreviewHash 生成
        ↓ production snapshot codec【新規】: UnavailableClaimSnapshotValidationCodecRegistry を置換
        ↓ Close / Cancel / Query UseCase【新規】 → ClaimFinalizationOperationV1（既存） → ClaimBatch（既存）
        ↓ ClaimPreparation画面（入力UIは既存985行。配線のみ【新規】）
```

### 設計原則

- `ServiceCodeResolver` と `ClaimCalculator` はDomainの**副作用なし純粋関数**（日付・乱数・I/O非依存。入出力を値で閉じる）。
- 端数規則・制度値はマスタ/ADRから供給し、計算コードにハードコードしない（ハードコード境界検査の対象を維持）。
- 各段は前段の出力**値**のみを受け取る。EFエンティティを算定に流さない。golden caseテストはseed JSONとテーブル駆動データだけで完結する。

### 実装順（垂直に薄く）

まず「基本報酬のみ・加算なし・負担上限なし」の最小ケースを seed → resolver → calculator → preview → 確定 まで**一気に貫通**させる。その後に加算 → 利用者負担 → R8切替・経過措置 の順で縦に積む。「土台だけ先に太らせる」旧計画の轍（両端肥大・中核空洞）を踏まない。

---

## 6. エラー処理

- **フェイルクローズの一貫適用**: マスタ不足・適用版不足・入力不足はすべて明示的な「算定不能」として失敗する（正本4.0）。推測値・空文字で続行しない。入口は既存の `ClaimPreparationReadiness` ゲート、算定段でも同じ原則を守る。
- **スコープ外の明示化**: 凍結領域（保護施設・基準該当等）の入力に対しては専用の「未対応（スコープ外）」算定不能理由を返す。黙って0円やスキップにしない。将来スライスの接続点を仕様として残す。
- 算定不能理由は行動可能な形（どの入力/マスタが不足か）で返す。ログに氏名・受給者証番号・保存先フルパスを出さない（ハード制約4）。

---

## 7. テスト戦略

- **golden caseテスト（最重要）**: 公式の計算例・手計算検証ケースをテーブル駆動で固定。基本報酬のみ → 加算あり → 負担上限あり → R8経過措置あり、の順にケースを積む。境界月2026-05/2026-06の切替テストを含む。
- **カバレッジ**: `Logic.Claim` 分岐100%（正本AC3-3）、Domain全体≧95%（既存方針）。
- **不変条件**: 明細単位数の合計 = 請求単位数、利用者負担 ≦ 証記載上限 ≦ 法定上限、確定済み `ClaimBatch` のappend-only性。
- **構造テスト（既存維持）**: オフライン検査・依存方向・ハードコード境界検査は現行のまま。schema v2 / validator / 保護施設契約テストも**削除せず**残す（凍結領域の契約を歯のある状態で保つ）。
- **seed実値の検証**: `ClaimMasterFileValidator` を通し、`sourceDocumentId` / `sourceSha256` 付きで投入。検証で一意に確定できない値は§3.2-3の通りスコープから外す。

---

## 8. 成功基準

1. 典型B型事業所の1ヶ月分の請求が、入力 → 算定プレビュー → 確定までUIから通る。
2. golden caseテストが公式計算例と一致し、全テスト緑・警告ゼロ・`./build/ci.sh` 緑。
3. seed 6ファイルのうち本スライス対象分に実値が入り、validator・出典メタデータ検証を通過している。
4. `docs/open-questions.md` に、スコープから外した未確定値が漏れなく起票されている。
5. 進捗が新実装計画のチェックボックスだけで判定できる。

---

## 9. 置換対象一覧

本specの承認をもって、以下の文書の**未完了部分の実行契約は失効**する。文書自体は履歴として残し、編集しない。

| 文書 | 扱い |
|---|---|
| `plans/2026-07-11-phase3-1-claim-calculation-and-input-foundation.md` | Task 1〜12の完了実績は事実として尊重。**Task 13〜28の実行契約は本specで置換**。未コミットのTask 11分割diffは破棄 |
| `plans/2026-07-12-phase3-1-task13-claim-master-seeds.md` | 置換（seed投入は本specのスコープ定義で再定義） |
| `plans/2026-07-14-phase3-1-task13-protected-facility-b-formula-and-source-inventory.md` | **凍結**（設計承認済み・実装着手せず。将来スライスで再評価） |
| `specs/2026-07-12-phase3-1-task13-claim-master-seed-design.md` | 置換 |
| `specs/2026-07-14-phase3-1-task13-protected-facility-b-formula-and-source-inventory-design.md` | 凍結（同上） |
| `plans/2026-06-29-phase3-1-calculation-engine.md` | 旧版（元々実行禁止。変更なし） |
| `specs/2026-07-11-phase3-1-claim-calculation-and-input-foundation-design.md` | 参照可だが、進め方・スコープは本specが優先 |

**引き続き有効**: `specs/2026-06-29-phase3-claim-data-design.md`（Phase 3全体設計・Global Constraints）、`specs/2026-07-13-phase3-1-task12-claim-master-schema-v2-design.md`（実装済み契約の記録）、`specs/2026-07-14-phase3-1-task12-prorated-units-optional-maximum-design.md`（同）、`specs/2026-07-11-phase3-1-task7-persistence-design.md`（完了記録）、Phase 3-2 / 3-3 の計画（未着手のまま将来対象）。

**完了記録として保存（実行対象外）**: `plans/2026-07-12-phase3-1-task7-persistence.md`、`plans/2026-07-13-phase3-1-task12-claim-master-schema-v2.md`、`plans/2026-07-14-phase3-1-task12-prorated-units-optional-maximum.md`。

---

## 10. 次のステップ

1. 本specのユーザーレビュー・承認。
2. writing-plans スキルで実装計画を作成（§4.4 ワークツリー浄化を最初のタスクに含める）。
3. 実装は superpowers の実行フロー（TDD・小さなコミット・受け入れ証跡）に従う。
