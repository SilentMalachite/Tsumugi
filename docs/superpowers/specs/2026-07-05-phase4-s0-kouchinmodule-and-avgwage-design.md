# Tsumugi Phase 4 / S0 設計 — KouchinModule v5 突合・手当モデル拡張・AC2-8 平均工賃確定

> **Source**: `07_ClaudeCode_Phase4実装指示_リリース準備_Tsumugi.md` §「運用値の確定」を接地したもの。
> **一次資料（2026-07-05 運用者提供）**: `KouchinModule_v5.bas`（マクロ）＋ `工賃計算ツール_v.xlsm`（計算テンプレート）。両者を突合した結果を反映した **v2**（v1 は .bas のみからの推測、.xlsm 検査で大幅修正）。
> **Status**: 設計合意済（2026-07-05）。実装計画は writing-plans スキルで本書から派生させる。
> **位置づけ**: Phase 4 全 5〜7 スライスのうちの **S0**（運用値確定枠）。他の (A)〜(E) スライスとは独立に着手・レビュー可能。
> **一次資料の取扱**: `.bas` / `.xlsm` は運用者提供物のためリポジトリには**コミットしない**（`.xlsm` にはサンプル氏名等が含まれる、`.bas` はコメントが Shift-JIS のためリポジトリ既定 UTF-8 と齟齬）。本書の §3 に要約を残し、原本はローカル `Desktop/` に保持する運用とする。

---

## 1. 目的

CHANGELOG「本番投入前に必須の deferred」の 2 件をクローズし、Phase 3-1（基本報酬区分算定）の入力を確実にする。

- **運用値-1**: KouchinModule v5（.bas + .xlsm）の実挙動を Tsumugi の Domain モデルに突合し、ADR 0012 の「暫定」ラベルを外す。**端数規則を含む全項目を確定**（v2 で新規確定）。
- **運用値-2**: 平均工賃月額（AC2-8）の分母・基準期間・控除定義を厚労省告示/通知の一次情報で確定し、`AverageWageMetric` の FIXME を解消する。

**副次目標**: 突合の過程で判明した**モデルの表現力不足**（Hourly + 手当合算・時給の期間変化・15 分単位）を、追記型設計哲学を維持したまま解消する。

---

## 2. 責務境界

- 本スライスは **報酬告示・国保連 CSV に触れない**（Phase 3 で扱う）。
- 手当（作業・職能・特別）は「工賃の配分結果に上乗せする支給額」であり、報酬側の加算とは別概念。両者を混同しない。
- 事業所レベルの手当ルール（作業手当日額・職能手当閾値表）は `WageSettings` の期間マスタで持つ。**シード JSON 化は本スライスでは行わない**（KouchinModule は自事業所固定運用のため）。
- 特別手当（利用者×月の任意支給）は独立 append-only レコード `WageAdjustment` で受ける。

---

## 3. KouchinModule v5 実挙動サマリ（`.xlsm` 突合済）

### 3.1 シート構造

- **固定シート 4 枚**: `操作` / `工賃明細 (配布用)` / `工賃集計` / `利用者`（計算テンプレート）
  - v1 で「説明」と記載していたのは誤読、正しくは `操作`
- **可変シート**: 利用者 1 名 = 1 シート。ダウンロード xlsx から丸コピーで生成される。
- **計算テーブル**: `利用者` シートの `AS1:BV41` を全利用者シートに数式＋書式で貼付し、各シートで独立計算する。

### 3.2 セル契約（`AS1:BV41` の実挙動要点）

| セル | 意味 | 数式概要 |
|---|---|---|
| `AU1` | 対象年月 | `A1` を参照 |
| `AU2` | 利用日数 | `AY5` 参照。実体は `COUNT(BH5:BH35)`（利用フラグ日数） |
| `AU3` | 利用者名 | `W3` を参照 |
| `AU4` | **工賃時給合計** | `AD40`（月合計、= Σ ROUND(時間×時給)）から要再チェック分を差引 |
| `AU5` | **作業手当** | `就労日数 BA5 × 500 円`（AS4="OK" 時は AG40 直接） |
| `AU6` | **職能手当** | `就労時間 BA4 >= 70h → 4000` / `>= 55h → 2000` / それ以外 → 0 |
| `AU7` | **工賃合計** | `SUM(AU4:AU6)` |
| `AY9` | 工賃時給最小単位 | `1/96 日 = 15 分`（入力制約に使用） |
| `AY7` | 既定の休憩時間 | 60 分 |
| `BA4` | 就労時間（h） | `((O40-R40-U40)/1440)*24`（打刻累計 − 休憩 − 除外時間 → 時間換算） |
| `BA5` | 就労日数 | `COUNT(BH5:BH35) − COUNT(BI5:BJ35)`（作業外・トライアル除外） |
| `BD5` | 日次工賃時給 | **`ROUND((X9*24)*AA9, 0)`** — 時間 × 時給を **四捨五入** |
| `AU12` | 時給変更検知 | `IF(MAX(AA9:AA39)=MIN(AA9:AA39),"なし","あり")` — 月中の時給変動を検知 |
| `AA9`〜`AA39` | 日次時給 | 利用者×日ごとに保持され、月中で変わり得る |

### 3.3 v1 からの主な修正

| 項目 | v1 の記述（誤） | v2 での確定 |
|---|---|---|
| AU5 | 「基本手当」（利用者ごと個別金額） | **「作業手当」**: 事業所単価（既定 500 円/日） × 就労日数 |
| AU6 | 「認定手当」（利用者ごと個別金額） | **「職能手当」**: 事業所レベルの時間閾値表（既定 55h/70h → 2000/4000 円） |
| 特別手当 | 計算に含まれる 3 番目の手当 | 計算テンプレートには**存在しない**。`工賃集計` G 列（VBA で手入力）だけの概念 |
| 端数規則 | 「不可視・open-question 継続」 | **`ROUND(x,0)` = 四捨五入（HalfUp）に確定** |
| 時給 | 事業所単価（暗黙前提） | 利用者×日 の属性。月中で変動し得る |
| 時間単位 | 不明 | **15 分単位**（入力バリデーションで検知） |

### 3.4 集計と月別ロールアップ

- `工賃集計!B4:H43` に利用者一覧を転記: `B=氏名 / D=工賃時給 / E=作業手当 / F=職能手当 / G=特別手当（手入力）/ H=利用時間`。
- `工賃集計!D51:O55` は年度単位の 4 系統 × 12 ヶ月マトリクス。年度起点は **4 月**（`If dataMonth >= 4 Then fy = dataYear`）。
- Tsumugi 側では月別ロールアップは既存の `QueryWageStatementUseCase` の期間指定で吸収する（新 UI は追加しない、§8）。

### 3.5 ADR 0012 突合結果（v2 確定表）

| 項目 | ADR 0012 暫定 | .xlsm 実挙動 | v2 での確定 |
|---|---|---|---|
| 年度起点月 | 4 | 4 | **4 に確定** |
| 主方式 | 4 方式並存・運用委任 | Hourly + 2 系統の規則手当 + 特別手当 | **既定 `Hourly` に確定**、他 3 方式は互換保持 |
| 端数規則 | FloorYen | ROUND | **`HalfUp` に変更**（`RoundingRule` に新規追加） |
| 余り処理 | LargestRemainder | 該当なし（按分せず個別計算） | **`LargestRemainder` 維持**（Hourly では実質未使用） |
| 手当 | 未モデル | 作業（日額単価）・職能（時間閾値表）・特別（利用者×月） | **`WageSettings` 拡張 + `WageAdjustment` の併用**（§4） |
| 時給 | 単一 Settings 前提 | 利用者×日 で変動可 | **`RecipientHourlyRate` 期間マスタで保持**（§6.4） |
| 時間単位 | 未指定 | 15 分単位 | **`WageSettings.HourUnitMinutes = 15` を保持し入力検証** |

---

## 4. 設計判断 — 手当モデルのハイブリッド化

### 4.1 v1 との差分（重要）

- v1 は「案 B: `WageAdjustment` 単独導入」で全 3 手当を吸収する設計だった。
- .xlsm 検査で **作業手当・職能手当は事業所レベルの規則**（利用者×月の個別金額ではない）と判明したため、これらは `WageSettings` に規則として持つ（案 C 相当）。
- **特別手当のみが利用者×月の任意金額**であり、`WageAdjustment`（案 B 相当）で受ける。
- したがって **案 B + 案 C のハイブリッド** が実挙動と最も整合する。

### 4.2 手当種別ごとの保持先

| 手当種別 | 保持先 | 根拠 |
|---|---|---|
| 作業手当 | `WageSettings.WorkAllowancePerDayYen`（int?）＋ 期間マスタで版管理 | 「就労日数 × 日額」の事業所単価 |
| 職能手当 | `WageSettings.SkillAllowanceTiers`（`IReadOnlyList<SkillAllowanceTier>`）＋ 期間マスタで版管理 | 「就労時間の閾値による段階固定」の事業所ルール |
| 特別手当 | `WageAdjustment`（新規 append-only エンティティ） | 利用者×月の任意支給 |

### 4.3 時給の保持

- 現行 `HourlyWageStrategy` は `WageInputs` で時間と時給を受け取るが、時給の**期間管理**は未整備。
- 実挙動では利用者×日の粒度で変動可能。追記哲学と整合させるため、**`RecipientHourlyRate`（利用者×期間の期間マスタ、`WageSettings` と同じ append-only スタイル）** を新設。
- `HourlyWageStrategy` は `RecipientHourlyRate` の実効値を引く純粋関数 `RecipientHourlyRatePolicy.Effective(records, recipientId, asOf)` を通す。
- 「月中で時給変動あり」は Application 層で `RecipientHourlyRatePolicy` を日単位で参照すれば自然に表現できる。

### 4.4 データ整合

- `WageAdjustment` は `WageStatement` 確定時に線形合算される（Strategy 出力額 + 手当規則額 + `WageAdjustment` 合計）。
- 手当規則額（作業＋職能）は Strategy 内で計算する。
- **不変条件**: 手当（規則も特別も）は「支給額」のため按分・原資 (`WageFund`) との Σ=原資 制約の**対象外**。原資チェックは Strategy の**素の出力額のみ**に適用する（既存挙動を維持）。

---

## 5. アーキテクチャ変更

依存方向は既存規律を維持（`App → Application → Domain`、`Infrastructure → Application/Domain`）。

```
src/
  Tsumugi.Domain/
    Entities/
      WageAdjustment.cs                    [新規] append-only 特別手当（利用者×月）
      RecipientHourlyRate.cs               [新規] append-only 時給期間マスタ（利用者×期間）
      WageSettings.cs                      [改修] WorkAllowancePerDayYen / SkillAllowanceTiers / HourUnitMinutes 追加
    Enums/
      RoundingRule.cs                      [改修] HalfUp 追加、既定を HalfUp へ
      WageAdjustmentType.cs                [新規] SpecialAllowance のみ（将来拡張余地は残す）
    ValueObjects/
      SkillAllowanceTier.cs                [新規] (MinHours, Yen) の record
    Logic/
      WageAdjustmentPolicy.cs              [新規] 実効値導出・合計（純粋関数）
      RecipientHourlyRatePolicy.cs         [新規] Effective(records, recipientId, asOf) 純粋関数
      RoundingPolicy.cs                    [新規] HalfUp / FloorYen などの丸め本体（純粋関数）
      AverageWageMetric.cs                 [改修] AC2-8 正式定義（§7）
      Wage/
        HourlyWageStrategy.cs              [改修] 15 分単位検証・HalfUp 丸め・作業＋職能規則額の加算
        (他 3 Strategy)                    [改修] 作業＋職能規則額のみ加算対応

  Tsumugi.Application/
    Abstractions/
      IWageAdjustmentRepository.cs         [新規]
      IRecipientHourlyRateRepository.cs    [新規]
    Dtos/
      WageAdjustmentDto.cs                 [新規]
      RecipientHourlyRateDto.cs            [新規]
      WageSettingsDto.cs                   [改修] 新フィールド追加
    UseCases/Wage/
      RecordWageAdjustmentUseCase.cs       [新規] New/Correction/Cancel の追記
      QueryWageAdjustmentUseCase.cs        [新規] 対象月の実効一覧
      SetRecipientHourlyRateUseCase.cs     [新規] 期間マスタ追記
      QueryRecipientHourlyRateUseCase.cs   [新規] 期間内の実効一覧
      ConfigureWageSettingsUseCase.cs      [改修] 新フィールド受入
      CalculateWagesUseCase.cs             [改修] 手当合算対応 + RecipientHourlyRate 引き当て
      CloseWagesUseCase.cs                 [改修] スナップショットに手当内訳を保持

  Tsumugi.Infrastructure/
    Persistence/
      WageAdjustmentRepository.cs          [新規]
      RecipientHourlyRateRepository.cs     [新規]
      Configurations/
        WageAdjustmentConfiguration.cs     [新規] (OfficeId, RecipientId, YearMonth, Type) WHERE Kind=New partial unique index
        RecipientHourlyRateConfiguration.cs[新規] (OfficeId, RecipientId, Period 開始) WHERE Kind=New partial unique index
        WageSettingsConfiguration.cs       [改修] 新フィールドの JSON カラム or 個別カラム
      AppendOnlyGuard.cs                   [改修] 監視型に WageAdjustment / RecipientHourlyRate を追加
    Migrations/
      <yyyyMMddHHmmss>_AddWageAdjustment.cs                [新規]
      <yyyyMMddHHmmss>_AddRecipientHourlyRate.cs           [新規]
      <yyyyMMddHHmmss>_ExtendWageSettingsAllowances.cs     [新規]

  Tsumugi.App/
    ViewModels/
      WageAdjustmentViewModel.cs           [新規] 月×利用者マトリクス編集
      RecipientHourlyRateViewModel.cs      [新規] 利用者×期間の時給編集
      WageFundSettingsViewModel.cs         [改修] 手当規則の編集 UI
    Views/
      WageAdjustmentView.axaml             [新規] タブ追加
      RecipientHourlyRateView.axaml        [新規] タブ追加
      WageFundSettingsView.axaml           [改修] 手当規則入力欄
      MainWindow.axaml                     [改修] タブ挿入
    CompositionRoot.cs                     [改修] 新 UseCase / Repository / VM を登録
```

---

## 6. データモデル

### 6.1 `WageSettings` の拡張

```csharp
public sealed record WageSettings : Entity
{
    // 既存フィールド ...

    // 新規（v2）
    public int? WorkAllowancePerDayYen { get; init; }                     // 作業手当日額（既定 500）
    public IReadOnlyList<SkillAllowanceTier> SkillAllowanceTiers { get; init; }
        = Array.Empty<SkillAllowanceTier>();                              // 例: [(55, 2000), (70, 4000)]
    public int HourUnitMinutes { get; init; } = 15;                       // 工賃時給の最小単位（分）
}

public sealed record SkillAllowanceTier(int MinHours, int Yen);
```

- `SkillAllowanceTiers` は EF Core 上は JSON 単一列（`WageSettings.SkillAllowanceTiersJson`）に展開（既存 `DateRangeJson` パターンに準拠）。
- 妥当性検証: `WorkAllowancePerDayYen >= 0`、`SkillAllowanceTiers` は `MinHours` 昇順・重複禁止・全 `Yen >= 0`、`HourUnitMinutes` は 1〜60 かつ 60 の約数（1/2/3/4/5/6/10/12/15/20/30/60）。

### 6.2 `RoundingRule` の拡張

```csharp
public enum RoundingRule
{
    FloorYen = 0,      // 既存: 切り捨て
    HalfUp = 1,        // 新規: 四捨五入
    Ceiling = 2,       // 追加（将来運用に備え）
}
```

- ADR 0012 で **既定を `HalfUp` に変更**。ただし列挙値の 0 は破壊的変更を避けるため **`FloorYen` のまま**（マイグレーションで新規追加を既定 `HalfUp` にする分岐は Migration 側で処理）。
- 新設 `RoundingPolicy.Round(decimal amount, RoundingRule rule)` 純粋関数で本体を集約する（既存の暗黙丸めを置換）。

### 6.3 `WageAdjustment` エンティティ

```csharp
public sealed record WageAdjustment : Entity
{
    public required Guid OfficeId { get; init; }
    public required Guid RecipientId { get; init; }
    public required YearMonth YearMonth { get; init; }
    public required WageAdjustmentType Type { get; init; }
    public required int AmountYen { get; init; }            // 0 以上
    public required RecordKind Kind { get; init; }          // New / Correction / Cancel
    public Guid? OriginId { get; init; }
    public string? Note { get; init; }                      // PII 禁止
}

public enum WageAdjustmentType
{
    SpecialAllowance = 0,   // 特別手当（.xlsm では工賃集計 G 列 手入力）
    // 将来: 業績賞与など任意支給を追加可能
}
```

**partial unique index**: `(OfficeId, RecipientId, YearMonth, Type) WHERE Kind=New`（既存 ADR 0015/0017 と同パターン）。

### 6.4 `RecipientHourlyRate` エンティティ

```csharp
public sealed record RecipientHourlyRate : Entity
{
    public required Guid OfficeId { get; init; }
    public required Guid RecipientId { get; init; }
    public required DateRange Period { get; init; }         // 開始〜（現在有効なら EndDate=Open）
    public required int HourlyYen { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? OriginId { get; init; }
    public string? Note { get; init; }
}
```

- `Effective(records, recipientId, asOf)` は「asOf が `Period` に含まれ、Kind の追記型合成を経た唯一の実効レコード」を返す純粋関数。
- **partial unique index**: `(OfficeId, RecipientId, Period 開始) WHERE Kind=New`。
- 月中変動は複数期間で表現できるため、`HourlyWageStrategy` は日単位で `Effective` を引く。

### 6.5 実効値導出（合成関数）

```csharp
public static class WageAdjustmentPolicy
{
    public static int EffectiveYen(
        IEnumerable<WageAdjustment> records,
        Guid recipientId, YearMonth ym, WageAdjustmentType type);

    public static int SumEffective(
        IEnumerable<WageAdjustment> records,
        Guid recipientId, YearMonth ym);
}

public static class RecipientHourlyRatePolicy
{
    public static int? EffectiveYen(
        IEnumerable<RecipientHourlyRate> records,
        Guid recipientId, DateOnly asOf);
}
```

### 6.6 `WageStatement` への影響

`WageStatement.LineItems` は既に `BasisSummary : string` を持つ。**エンティティ本体の変更は不要**で、`BasisSummary` を「時給計算 12,000 円 + 作業手当 7,500 円 + 職能手当 2,000 円 + 特別手当 1,000 円 = 22,500 円」のような形式で内訳保持する。

---

## 7. AverageWageMetric の正式化（v1 と同内容）

現状: `AverageWageDenominator` に `TotalRecipients` / `ActiveRecipients` の 2 択、コメントに「暫定」と明記。

### 7.1 一次情報の突合対象

- 厚労省告示「就労継続支援B型の平均工賃月額の算定方法について」（社会・援護局長通知、直近改正）
- 加算算定に用いる「前年度平均工賃月額」の分母定義（延べ利用者 or 実利用者、常勤換算の有無、途中退所者の扱い）

### 7.2 実装方針

一次資料入手までは現行の 2 択構造を維持し、確定後にコメントの「暫定」表記を除去、テスト側で正式定義を固定する。もし通知が常勤換算・除外者を伴う場合は `AverageWageDenominator` の列挙値追加・`Calculate` に「除外対象月数」パラメータ追加で対応（純粋関数を維持）。

一次資料が入手不能な期間は S0 内では**構造整備のみ**とし、AC2-8 のクローズは実資料入手後（Phase 4 後段）に回す（open-questions を「保留・入手先明記」で更新）。

---

## 8. UI 変更

- **`WageFundSettingsView`（改修）**: 既存の原資 + 期間 + 方式に加え、**作業手当日額**・**職能手当閾値表**（追加/削除可能な行）・**工賃時給最小単位** の入力欄。
- **`RecipientHourlyRateView`（新規）**: 利用者選択 → 期間別時給の一覧・追記 UI。
- **`WageAdjustmentView`（新規）**: 月 × 利用者 × 特別手当のマトリクス編集。
- **`WageCalculationView`（既存）**: プレビュー欄に手当合算後の総額と内訳（時給計算額 / 作業 / 職能 / 特別）を並記。
- 月別ロールアップ画面は追加しない（YAGNI、§v1 と同）。
- アクセシビリティ規約（CLAUDE.md §ハード制約 5）を維持: キーボード完結・タブ順・フォント拡大追従。

---

## 9. テスト戦略

### Domain
- **`RoundingPolicy`**: HalfUp / FloorYen / Ceiling 各境界（0.5 の丸め、負数扱い、`decimal` 精度）。
- **`SkillAllowanceTier` バリデーション**: 昇順違反・重複・負数拒否のテーブル駆動。
- **`WageAdjustmentPolicy.EffectiveYen`**: New のみ / New→Correction / New→Correction→Cancel / Cancel 単独 / Type 別独立の各境界。
- **`WageAdjustmentPolicy.SumEffective`**: 合計、Cancel 適用後 0 円。
- **`RecipientHourlyRatePolicy.EffectiveYen`**: 期間重複時の最新採用・境界日・Cancel 適用・複数追記型のケース。
- **`WageAdjustment` エンティティ**: `AmountYen < 0` 拒否、`Kind != New` かつ `OriginId is null` 拒否。
- **`RecipientHourlyRate` エンティティ**: `HourlyYen < 0` 拒否、Period 妥当性、Kind と OriginId の整合。
- **`AverageWageMetric.Calculate`**: 分母切替・空リスト・除外者ゼロ。
- **`HourlyWageStrategy`（改修）**:
  - **KouchinModule v5 突合ケース**: 就労日数 15 日 × 就労時間 26h × 時給 350 円 → 期待値: 工賃時給 = ROUND(26×350) = 9,100 円 + 作業手当 15×500=7,500 円 + 職能手当 0（26h < 55h）= 合計 16,600 円。**AY9/BA4/BA5/AU7 の実挙動と一致することをテーブル駆動で固定**。
  - 15 分単位未満の時間入力は拒否（`ArgumentException`）。
  - 時給が月中変動するケース（RecipientHourlyRate で 2 期間定義）でも日単位に引き当てて合算。
  - 職能手当閾値（55h/70h）の境界。

### Application
- `RecordWageAdjustmentUseCase` / `SetRecipientHourlyRateUseCase`: New/Correction/Cancel の追記・監査ログ記録・空 actor 拒否。
- `QueryWageAdjustmentUseCase` / `QueryRecipientHourlyRateUseCase`: Domain Policy の合成結果と一致。
- `ConfigureWageSettingsUseCase`（改修）: 新フィールドの受入・バリデーション。
- `CalculateWagesUseCase`（改修）: 手当あり/なし × 時給変動あり/なし × 全 4 方式 の組合せで期待値一致。
- `CloseWagesUseCase`（改修）: 確定時の内訳保持と再確定シナリオ。

### Infrastructure
- `WageAdjustmentRepository` / `RecipientHourlyRateRepository` の追記型往復。
- partial unique index 制約テスト（二重 New 挿入が `DbUpdateException`）。
- `AppendOnlyGuard` に新 2 エンティティを含めた回帰テスト。
- `WageSettings` の JSON 展開・逆展開のラウンドトリップ。

### App（ViewModel）
- `WageFundSettingsViewModel`（改修）: 手当規則の追加/削除/バリデーションエラー表示。
- `RecipientHourlyRateViewModel`: 利用者選択 → 期間追加 → 保存の一気通貫。
- `WageAdjustmentViewModel`: 事業所選択 → 月選択 → マトリクス編集 → 保存。
- `WageCalculationViewModel`: 手当合算後の総額表示と内訳。

### CI / 品質ゲート
- `build/ci.sh` は既存のまま（Domain 95% 閾値を維持）。
- 既存の CJK フォント問題により PDF テストの substring assertion 制限は S0 スコープ外（S1 でフォント埋込により解消）。

---

## 10. ADR 影響

### ADR 0012（改修・確定化）

「暫定」ラベルを外し、以下を確定へ書き換え。

- 年度起点月 = 4
- 既定方式 = `Hourly`（他 3 方式は互換保持）
- 端数 = **`HalfUp`（変更）**、`RoundingPolicy` 経由で適用
- 余り = `LargestRemainder` 維持（Hourly では実質未使用）
- **手当ルール**: `WageSettings` に `WorkAllowancePerDayYen` / `SkillAllowanceTiers` / `HourUnitMinutes` を持つ
- **特別手当**: `WageAdjustment` エンティティで受ける
- **時給の期間管理**: `RecipientHourlyRate` エンティティで持つ

### ADR 0018（新規）

`WageAdjustment` を append-only 特別手当レコードとして導入。背景（.xlsm 突合結果）・選択肢（案 A/B/C）・ハイブリッド採用理由を記す。

### ADR 0019（新規）

`RecipientHourlyRate` を利用者×期間の時給期間マスタとして導入。背景（月中変動を追記型で表現する必要）・`WageSettings` 併存の理由（前者は事業所単価・後者は利用者単価）を記す。

---

## 11. 未確定事項の更新（`docs/open-questions.md`）

### v1 から v2 でクローズ

- ~~[ ] KouchinModule `AS1:BV41` の Excel 数式内部（端数・時間の丸め）~~ → **確定**（HalfUp・15 分単位）
- ~~[ ] 特別手当の性格~~ → **確定**（利用者×月の任意支給、`WageAdjustment` で受ける）

### v2 でも継続

- [ ] **平均工賃月額の正式定義**: §7 記載の通り、一次資料入手時にクローズ。
- [ ] **職能手当閾値の妥当性**: 55h/70h は 2025 年時点の運用者設定値。厚労省告示由来ではないため、他事業所適用時に閾値・金額を UI から編集可能とする（本設計で対応済み）。

`docs/open-questions.md` の Phase 2 セクションは、**確定した箇所を `[x]` へ移し、残余は「入手先・保留理由」を追記して継続**。

---

## 12. 受け入れ基準（Phase 4 ACへの寄与）

`07_ClaudeCode_Phase4実装指示_リリース準備_Tsumugi.md` §7 の以下を本スライスでクローズ／前進させる:

- **AC4-13**（ADR 0012 KouchinModule 突合正式化）: ✅ **完全達成**（端数含め全項目確定）。
- **AC4-14**（AC2-8 平均工賃月額 正式定義）:
  - 一次資料入手済の場合: ✅ 完了。
  - 未入手の場合: 部分達成（`AverageWageMetric` 構造整備は完了・正式値差し替えは保留）。

**S0 完了の必要十分条件**（`build/ci.sh` 緑 + 以下全項目）:
1. `WageSettings` が `WorkAllowancePerDayYen` / `SkillAllowanceTiers` / `HourUnitMinutes` を持ち、既存版のマイグレーションが緑。
2. `RoundingRule.HalfUp` が導入され、`RoundingPolicy.Round` が全 Strategy から使われる。
3. `RecipientHourlyRate` エンティティ・Policy・Repository・Migration が実装され、`HourlyWageStrategy` から引き当てられる。
4. `WageAdjustment` エンティティ・Policy・Repository・Migration が実装され、既存 `AppendOnlyGuard` に取り込まれている。
5. `CalculateWagesUseCase` / `CloseWagesUseCase` が Strategy 出力 + 規則手当 + `WageAdjustment` の合算に対応し、**KouchinModule 期待値テーブル駆動テスト**（`.xlsm` 実数値との一致）が緑。
6. `WageFundSettingsView` の改修と `WageAdjustmentView` / `RecipientHourlyRateView` の新設 UI が `MainWindow` に追加され、ViewModel テストが緑。
7. ADR 0012 が「確定」へ書き換わり、ADR 0018 / 0019 が新設されている。
8. CHANGELOG「本番投入前に必須の deferred」から KouchinModule 突合が除去され、AC2-8 の状態が更新されている。
9. `docs/open-questions.md` の該当項目が §11 に沿って更新済み。

---

## 13. スコープ外（S0 では扱わない）

- Phase 4 (A) フォント埋込・QuestPDF ライセンス確定（→ S1）
- Phase 4 (B) 暗号化 ADR 0003 決着・バックアップ運用化（→ S3）
- Phase 4 (C) UI 補完 3 点・ContractedProvider ADR（→ S4）
- Phase 4 (D) 発行スクリプト・初回セットアップ・運用ガイド（→ S5/S6）
- Phase 4 (E) bulk operations 禁止スキャナ・NetArchTest ADR（→ S2）
- 手当マスタ化（単価テーブル・シード JSON 化）: `WageSettings` の期間マスタで十分。シード JSON 化は Phase 5+ 任意。
- KouchinModule の月別ロールアップ画面: 既存の月次サマリで代替可能なため追加しない（§8）。
- 時給の遡及更新 UI: 追記型で表現可能なため、Correction/Cancel は API 経由のみ（S0 UI は追記のみ）。

---

## 14. リスク

| # | リスク | 影響 | 緩和策 |
|---|---|---|---|
| R1 | `WageSettings` 拡張により既存 Phase 2 マイグレーションと衝突 | ビルド不能・既存 DB マイグレーション失敗 | 新規カラムは全て nullable / 既定値付き。既存レコードは既定値で埋める migration をテストで検証。 |
| R2 | `RoundingRule` 既定変更が既存テストを赤化 | Phase 2 テスト回帰 | 既定を変えるのは新規作成分のみ。既存 `FloorYen` 明示済みテストは維持。差分明示の commit を用意。 |
| R3 | AC2-8 一次資料未入手 → 平均工賃月額が正式化できない | Phase 3-1（基本報酬区分）が入力待ち | S0 で構造整備を完了させ、値差し替えを 1 コミットで実現できる状態にする。 |
| R4 | `RecipientHourlyRate` 追加により既存 `HourlyWageStrategy` の入力構造が変わる | Phase 2 呼出元の全変更が必要 | 既存 `WageInputs` に「日単位時給」を追加し、後方互換のため `WageInputs.HourlyYen`（従来値）はフォールバックとして残す。 |
| R5 | UI 追加 2 タブによる操作動線の複雑化 | 現場受入抵抗 | 既定タブ順を「実績→原資設定→計算→明細」の順で維持し、時給/特別手当は「原資設定」タブ内のサブタブとして配置することも検討（S0 実装時に判断）。 |
| R6 | .bas / .xlsm を非コミットとすることで根拠が散逸するリスク | 保守時に一次資料と設計の対応が追えなくなる | 本 §3 に要点を明記し、実装時のテスト固定値（16,600 円等）に .xlsm 実挙動由来のコメントを付す。 |

---

## 15. 参照

- `07_ClaudeCode_Phase4実装指示_リリース準備_Tsumugi.md` §「運用値の確定」
- `docs/decisions/0012-wage-calculation-strategy.md`（本スライスで確定へ書き換え）
- `docs/decisions/0015-dailyrecord-duplicate-new-index.md` / `0017-wagefund-duplicate-new-index.md`（partial unique index パターン）
- `docs/open-questions.md` § Phase 2 工賃計算
- 一次資料（運用者提供 2026-07-05、リポジトリ非コミット）: `KouchinModule_v5.bas` / `工賃計算ツール_v.xlsm`
