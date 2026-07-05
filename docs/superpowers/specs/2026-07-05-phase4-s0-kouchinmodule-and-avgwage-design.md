# Tsumugi Phase 4 / S0 設計 — KouchinModule v5 突合・WageAdjustment 導入・AC2-8 平均工賃確定

> **Source**: `07_ClaudeCode_Phase4実装指示_リリース準備_Tsumugi.md` §「運用値の確定」を接地したもの。一次資料 `KouchinModule_v5.bas`（運用者提供、2026-07-05）から挙動を復元。
> **Status**: 設計合意済（2026-07-05）。実装計画は writing-plans スキルで本書から派生させる。
> **位置づけ**: Phase 4 全 5〜7 スライスのうちの **S0**（運用値確定枠）。他の (A)〜(E) スライスとは独立に着手・レビュー可能。
> **着手規律**: 端数規則および `特別手当` の性格（詳細は §11）は本設計での確定範囲外。該当箇所は暫定既定 + open-questions 継続として実装する。

---

## 1. 目的

CHANGELOG「本番投入前に必須の deferred」の 2 件をクローズし、Phase 3-1（基本報酬区分算定）の入力を確実にする。

- **運用値-1**: KouchinModule.bas v5 の実挙動を Tsumugi の Domain モデルに突合させ、ADR 0012 の「暫定」ラベルを外す。
- **運用値-2**: 平均工賃月額（AC2-8）の分母・基準期間・控除定義を厚労省告示/通知の一次情報で確定し、`AverageWageMetric` の FIXME を解消する。

**副次目標**: .bas 突合の過程で判明した**モデルの表現力不足**（Hourly + 3 手当の合算が単一 `Method` に収まらない）を、追記型設計哲学を維持したまま解消する。

## 2. 責務境界

- 本スライスは **報酬告示・国保連 CSV に触れない**（Phase 3 で扱う）。
- 手当（基本／認定／特別）は「工賃の配分結果に上乗せする支給額」であり、報酬算定側の加算とは別概念。両者を混同しない。
- 手当のマスタ化（単価テーブル・シード JSON 化）は本スライスでは行わない。**利用者ごと・月ごとに個別金額として append-only 記録**する（.bas の実挙動と同型）。

---

## 3. KouchinModule v5 実挙動サマリ

### 3.1 シート構造

- **固定シート 4 枚**: `説明` / `利用者`（計算テンプレート）/ `工賃集計` / `工賃明細 (配布用)`
- **可変シート**: 利用者 1 名 = 1 シート。ダウンロード xlsx を丸ごとコピーして生成される。
- **計算テーブル**: `利用者` シートの `AS1:BV41` を全利用者シートに数式＋書式で貼付し、各シートで独立計算する。

### 3.2 セル契約（AU 列）

| セル | 意味 |
|---|---|
| `AU2` | 対象年月（例 `2026-05`） |
| `AU3` | 利用者名 |
| `AU4` | 工賃時給計算額（1 名合計・時給ベース） |
| `AU5` | **基本手当**（利用者ごと・月ごとに個別記入） |
| `AU6` | **認定手当**（同上） |
| `AU7` | 1 名合計（= AU4 + AU5 + AU6 + 特別手当） |

`AS1:BV41` の Excel 数式内部の端数規則までは .bas から不可視。**現物 `.xlsx` 未提供のため、端数と余りは ADR 0012 の既定を維持しつつ open-questions に残す**（§11）。

### 3.3 集計と月別ロールアップ

- `工賃集計!B4:H43` に利用者一覧を転記: `B=氏名 / D=工賃時給 / E=基本手当 / F=認定手当 / G=特別手当（手入力）/ H=利用時間`。
- `工賃集計!D51:O55` は年度単位の 4 系統 × 12 ヶ月マトリクス。**年度起点は 4 月**（`If dataMonth >= 4 Then fy = dataYear`）。年度切替時は確認ダイアログで旧年度データを退避せずクリア。
- Tsumugi 側では月別ロールアップは既存の `QueryWageStatementUseCase` + 集計 VM で吸収する（新 UI は追加しない、§8）。

### 3.4 ADR 0012 突合結果

| 項目 | ADR 0012 暫定 | .bas 実挙動 | 本設計での確定 |
|---|---|---|---|
| 年度起点月 | 4 | 4 | **4 に確定** |
| 主方式 | Piece/Hourly/Fixed/Equal 並存・運用委任 | Hourly + 3 手当加算 | **既定を `Hourly` に確定**（他 3 方式は Phase 5+ の互換用に残置） |
| 端数規則 | FloorYen | 不可視（Excel 数式内） | **FloorYen 維持** + open-question 継続 |
| 余り処理 | LargestRemainder | 概念なし（按分ではなく個別計算） | **LargestRemainder 維持** + Hourly では実質未使用 |
| 手当 | 未モデル | 3 系統（基本／認定／特別） | **`WageAdjustment` 新エンティティで表現**（§5） |

---

## 4. 設計判断 — 手当のモデリング

### 4.1 選択と根拠

**採用: 案 B — 独立 `WageAdjustment` レコード**（append-only）。

- .bas の AU5/AU6 は利用者シートごとの個別値であり、事業所単価ではないため、`WageSettings` に単価欄を追加する案 C はデータ実態と乖離する。
- 5 番目の Strategy として実装する案 A は ADR 0012 の「4 方式で固定・プラグイン拡張は YAGNI」を破る。
- 独立レコードとすることで、任意の `Method`（現行 4 方式）に上乗せ可能となり、将来の手当種別追加（処遇改善など）も追記のみで拡張できる。

### 4.2 データ整合

- `WageAdjustment` は **`WageStatement` 確定時に線形合算**される（Strategy 出力額 + `WageAdjustment` 合計）。
- `Correction` 系統: `WageAdjustment` 自身も追記型（`Kind=New | Correction | Cancel`）。実効値は既存 `WageStatementPolicy` と同じスタイルで導出する純粋関数 `WageAdjustmentPolicy.Effective` を提供。
- **不変条件**: 手当は「支給額」のため按分・原資 (`WageFund`) との Σ=原資 制約の対象外。原資チェックは Strategy 出力額のみに適用する（既存挙動を維持）。

---

## 5. アーキテクチャ変更

依存方向は既存規律を維持（`App → Application → Domain`、`Infrastructure → Application/Domain`）。

```
src/
  Tsumugi.Domain/
    Entities/
      WageAdjustment.cs                    [新規] append-only 手当レコード
    Enums/
      WageAdjustmentType.cs                [新規] BasicAllowance / CertifiedAllowance / SpecialAllowance
    Logic/
      WageAdjustmentPolicy.cs              [新規] 実効値導出（Effective/Sum）純粋関数
      AverageWageMetric.cs                 [改修] AC2-8 正式定義へ差し替え（§7）
      Wage/
        (既存 4 Strategy は変更なし)

  Tsumugi.Application/
    Abstractions/
      IWageAdjustmentRepository.cs         [新規]
    Dtos/
      WageAdjustmentDto.cs                 [新規]
    UseCases/Wage/
      RecordWageAdjustmentUseCase.cs       [新規] New/Correction/Cancel の追記
      QueryWageAdjustmentUseCase.cs        [新規] 対象月の実効一覧
      CalculateWagesUseCase.cs             [改修] Strategy 出力 + WageAdjustment 実効合計をマージ
      CloseWagesUseCase.cs                 [改修] スナップショットに手当内訳を保持

  Tsumugi.Infrastructure/
    Persistence/
      WageAdjustmentRepository.cs          [新規]
      Configurations/
        WageAdjustmentConfiguration.cs     [新規] (OfficeId, RecipientId, YearMonth, Type) WHERE Kind=1 partial unique index
      AppendOnlyGuard.cs                   [改修] 監視型に WageAdjustment を追加
    Migrations/
      <yyyyMMddHHmmss>_AddWageAdjustment.cs [新規]

  Tsumugi.App/
    ViewModels/
      WageAdjustmentViewModel.cs           [新規] 月×利用者マトリクス編集
    Views/
      WageAdjustmentView.axaml             [新規] タブ追加
      MainWindow.axaml                     [改修] タブ挿入
    CompositionRoot.cs                     [改修] 新 UseCase / Repository / VM を登録
```

---

## 6. データモデル

### 6.1 `WageAdjustment` エンティティ

```csharp
public sealed record WageAdjustment : Entity
{
    public required Guid OfficeId { get; init; }
    public required Guid RecipientId { get; init; }
    public required YearMonth YearMonth { get; init; }
    public required WageAdjustmentType Type { get; init; }
    public required int AmountYen { get; init; }            // 0 以上、上限は Guard で 1,000,000 円未満
    public required RecordKind Kind { get; init; }          // New / Correction / Cancel
    public Guid? OriginId { get; init; }                    // Correction/Cancel 時の元 New の Id
    public string? Note { get; init; }                      // 支給根拠のメモ（PII 禁止）
}

public enum WageAdjustmentType
{
    BasicAllowance = 0,        // 基本手当（AU5 相当）
    CertifiedAllowance = 1,    // 認定手当（AU6 相当）
    SpecialAllowance = 2,      // 特別手当（工賃集計 G 列相当、手入力）
}
```

### 6.2 実効値導出

```csharp
public static class WageAdjustmentPolicy
{
    // recipient×month×type ごとに最新 New → Corrections → 最終 Cancel の順で合成
    // Cancel 到来後は 0 円と見なす
    public static int EffectiveYen(
        IEnumerable<WageAdjustment> records,
        Guid recipientId, YearMonth ym, WageAdjustmentType type);

    public static int SumEffective(
        IEnumerable<WageAdjustment> records,
        Guid recipientId, YearMonth ym);
}
```

### 6.3 partial unique index

`(OfficeId, RecipientId, YearMonth, Type) WHERE Kind=New` を SQLite partial index で強制（既存 `WageFundDuplicateNewIndex` / `DailyRecordDuplicateNewIndex` と同スタイル、ADR 0015/0017 参照）。

### 6.4 `WageStatement` への影響

`WageStatement` は既にスナップショット型で確定金額を保持する。合算後の金額をそのまま保存すればよく、**エンティティ本体の変更は不要**。ただし内訳可視化のため `WageStatement.LineItems` に「手当内訳文字列」を含める（Domain 側は既に `BasisSummary : string` があるためこのフィールドを流用）。

---

## 7. AverageWageMetric の正式化

現状: `AverageWageDenominator` に `TotalRecipients` / `ActiveRecipients` の 2 択、コメントに「暫定」と明記。

### 7.1 一次情報の突合対象

- 厚労省告示「就労継続支援B型の平均工賃月額の算定方法について」（社会・援護局長通知、直近改正）
- 加算算定に用いる「前年度平均工賃月額」の分母定義（延べ利用者 or 実利用者、常勤換算の有無、途中退所者の扱い）

### 7.2 実装方針

一次資料入手までの間は現行の 2 択構造を維持したまま、**確定後にコメントの `[Obsolete("暫定")]` 相当表記を除去**し、テスト側で正式定義を固定する。もし通知が **常勤換算・除外者** を伴う場合は次の 2 点を追加する:

- `AverageWageDenominator` に `ActiveExcludingShortStay` 等の列挙値を追加
- `AverageWageMetric.Calculate` に「除外対象月数」パラメータを追加（純粋関数を維持）

一次資料が本スライス着手時に入手不能の場合は、S0 内では **「実装は据え置き・テストを一次資料の仕様で書き足し可能な形に整理」** のみを行い、AC2-8 のクローズは Phase 4 の後段（実資料入手後）に回す（open-questions を「保留・入手先明記」で更新）。

---

## 8. UI 変更（最小）

- 新規 `WageAdjustmentView` タブを追加し、月×利用者のマトリクス編集（3 手当 × N 名）。
- 既存 `WageCalculationView` のプレビュー欄には手当合算後の総額と、内訳（時給計算額 / 3 手当）を並記。
- **月別ロールアップ画面は追加しない**。KouchinModule の `D51:O55` は既存 `QueryWageStatementUseCase` の期間指定で代替可能なため、専用 VM 追加を避ける（YAGNI）。

アクセシビリティ規約（ADR-independent の CLAUDE.md §ハード制約 5）を維持: キーボード完結・タブ順・フォント拡大追従。

---

## 9. テスト戦略

### Domain
- `WageAdjustmentPolicy.EffectiveYen`: New のみ / New→Correction / New→Correction→Cancel / Cancel 単独 / Type 別独立の各境界。
- `WageAdjustmentPolicy.SumEffective`: 3 種同時、0 件、Cancel 適用後 0 円。
- `WageAdjustment` エンティティ: `AmountYen < 0` 拒否、上限（1,000,000 円未満）違反拒否、`Kind != New` かつ `OriginId is null` 拒否。
- `AverageWageMetric` の Calculate: 分母切替・空リスト・除外者ゼロ。
- `WageCalculator`（改修）: Strategy 出力 + `WageAdjustmentPolicy.SumEffective` の合算結果が期待値と一致するテーブル駆動テスト。**KouchinModule v5 突合ケース**として「Hourly + 各手当あり」の期待値を含める。

### Application
- `RecordWageAdjustmentUseCase`: New/Correction/Cancel の追記、監査ログ記録、`ArgumentException`（空 actor 拒否）。
- `QueryWageAdjustmentUseCase`: 対象月の実効一覧が Domain Policy の合成結果と一致。
- `CalculateWagesUseCase`（改修）: 既存 4 方式 × 手当あり／なし の 8 組合せで Snapshot 一致。
- `CloseWagesUseCase`（改修）: 確定時の内訳保持と再確定シナリオ。

### Infrastructure
- `WageAdjustmentRepository` の追記型往復。
- partial unique index 制約（`(OfficeId, RecipientId, YearMonth, Type) WHERE Kind=New` の二重挿入が `DbUpdateException`）。
- `AppendOnlyGuard` に `WageAdjustment` を含めた回帰テスト。

### App（ViewModel）
- `WageAdjustmentViewModel`: 事業所選択 → 月選択 → マトリクス編集 → 保存の一気通貫。
- `WageCalculationViewModel`: 手当合算後の総額表示。

### CI / 品質ゲート
- `build/ci.sh` は既存のまま（Domain 95% 閾値を維持）。
- 既存の CJK フォント問題により PDF テストの substring assertion 制限は S0 スコープ外（別スライス S1 でフォント埋込により解消）。

---

## 10. ADR 影響

- **ADR 0012**: 本設計により「暫定」ラベルを外し、以下を確定へ書き換え。
  - 年度起点月 = 4（KouchinModule v5 突合済）
  - 既定方式 = `Hourly`（他 3 方式は互換のため保持）
  - 端数 = `FloorYen` **維持**（Excel 数式非可視のため open-question 継続）
  - 余り = `LargestRemainder` **維持**（Hourly では実質未使用）
- **ADR 0018（新規）**: `WageAdjustment` を append-only 手当レコードとして導入。決定の背景（.bas 突合結果）・選択肢（案 A/B/C）・却下理由を記す。
- **他 ADR**: 変更なし。

---

## 11. 未確定事項（`docs/open-questions.md` 継続）

本スライス完了後も残す open-question:

- [ ] **KouchinModule `AS1:BV41` の Excel 数式内部**: 端数規則（切上/切捨/四捨五入）・時給×時間の丸めタイミング。運用者から `.xlsx` 現物を追加入手できたら確定。
- [ ] **特別手当（AU7 - AU4 - AU5 - AU6 分）の性格**: 賞与・褒賞など、記入根拠が現場運用に依存。Tsumugi では `SpecialAllowance` として同型受入するが、支給根拠の記入ガイドは運用ガイド（Phase 4 (D)）で案内。
- [ ] **平均工賃月額の正式定義**: §7 記載の通り、一次資料入手時にクローズ。

`docs/open-questions.md` の Phase 2 セクション該当項目は、**確定した箇所のみ `[x]` へ移し、残余は「入手先・保留理由」を追記して継続**。

---

## 12. 受け入れ基準（Phase 4 ACへの寄与）

`07_ClaudeCode_Phase4実装指示_リリース準備_Tsumugi.md` §7 の以下を本スライスでクローズ／前進させる:

- **AC4-13**（ADR 0012 KouchinModule 突合正式化）: ✅ 完了（§10 参照）。ただし端数規則は保留と明記。
- **AC4-14**（AC2-8 平均工賃月額 正式定義）: 
  - 一次資料入手済の場合: ✅ 完了。
  - 未入手の場合: 部分達成（`AverageWageMetric` 構造整備は完了・正式値差し替えは保留）。

**S0 完了の必要十分条件**（`build/ci.sh` 緑 + 以下全項目）:
1. `WageAdjustment` エンティティ・Policy・Repository・Migration が実装され、既存 AppendOnlyGuard に取り込まれている。
2. `CalculateWagesUseCase` / `CloseWagesUseCase` が Strategy + WageAdjustment 合算に対応し、KouchinModule 期待値テーブル駆動テストが緑。
3. `WageAdjustmentView` タブが `MainWindow` に追加され、事業所選択 → 月選択 → 保存の ViewModel テストが緑。
4. ADR 0012 が「確定」へ書き換わり、ADR 0018 が新設されている。
5. CHANGELOG「本番投入前に必須の deferred」から KouchinModule 突合が除去され、AC2-8 の状態が更新されている。
6. `docs/open-questions.md` の該当項目が § 11 に沿って更新済み。

---

## 13. スコープ外（S0 では扱わない）

- Phase 4 (A) フォント埋込・QuestPDF ライセンス確定（→ S1）
- Phase 4 (B) 暗号化 ADR 0003 決着・バックアップ運用化（→ S3）
- Phase 4 (C) UI 補完 3 点・ContractedProvider ADR（→ S4）
- Phase 4 (D) 発行スクリプト・初回セットアップ・運用ガイド（→ S5/S6）
- Phase 4 (E) bulk operations 禁止スキャナ・NetArchTest ADR（→ S2）
- 手当マスタ化（単価テーブル・シード JSON）: 現場運用が「利用者ごと個別金額」のため、マスタ化は Phase 5+ の任意。
- KouchinModule の月別ロールアップ画面: 既存の月次サマリで代替可能なため追加しない（§8）。

---

## 14. リスク

| # | リスク | 影響 | 緩和策 |
|---|---|---|---|
| R1 | `.xlsx` 現物未入手 → 端数規則未確定のまま S0 完了 | 実運用で 1 円差の齟齬が出る可能性 | ADR 0012 に「端数規則は暫定継続」を明記、open-question を保留化。運用ガイドで「現物 Excel との突合手順」を用意（Phase 4 (D)）。 |
| R2 | AC2-8 一次資料未入手 → AverageWageMetric が正式化できない | Phase 3-1（基本報酬区分）が入力待ちになる | S0 で構造整備（分母切替に強い形）を完了させ、値の差し替えを 1 コミットで実現できる状態にする。 |
| R3 | `WageAdjustment` 追加により既存 Phase 2 テストが機能変更で赤化 | 実装コスト増 | Strategy 出力 + WageAdjustment 合計 = 従来総額（手当が空なら現行と同一）となる合成にする。既存テストは手当空ケースとして通り続けるべき。 |
| R4 | 月×利用者マトリクス UI が入力手間を招く | 現場受入抵抗 | .bas 相当の「利用者列 × 3 手当 × 1 月」表を維持し、一括ペースト（クリップボード貼付）を将来対応（YAGNI 判断で S0 では単セル入力のみ）。 |

---

## 15. 参照

- `07_ClaudeCode_Phase4実装指示_リリース準備_Tsumugi.md` §「運用値の確定」
- `docs/decisions/0012-wage-calculation-strategy.md`（本スライスで確定へ書き換え）
- `docs/decisions/0015-dailyrecord-duplicate-new-index.md` / `0017-wagefund-duplicate-new-index.md`（partial unique index パターン）
- `docs/open-questions.md` § Phase 2 工賃計算
- `KouchinModule_v5.bas`（一次資料、運用者提供 2026-07-05）
