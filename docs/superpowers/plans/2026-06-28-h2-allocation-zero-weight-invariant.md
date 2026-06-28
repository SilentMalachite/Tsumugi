# H-2: ゼロ重み時の Σ配分=原資 不変条件回復 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `AllocationPolicy.Allocate` が「全員0分など `totalWeight = 0` ∧ `totalYen > 0`」のとき `Σ配分 ≠ 原資` を返してしまう不具合を修正し、AC2-4「按分方式で Σ配分＝原資 が常に成立」を `RemainderPolicy` 別に決定論で満たす。同時に上位 Strategy/UseCase 経由でも回帰しないよう契約を統一する。

**Architecture:**
不変条件を Domain (`AllocationPolicy`) 単独で機械的に守る設計に統一する。`totalYen > 0` かつ重み総和ゼロのケースは `RemainderPolicy` で挙動が分岐:
- `LargestRemainder` → `InvalidOperationException` を投げて「不可」を明示（呼び出し側は UseCase 経由で UI に伝搬）。
- `ReserveToOffice` → 全額を `officeReserveKey` の行に集約（既存 `ReserveToOffice` の自然な拡張）。
- `totalYen == 0` → 全員 0 円（既存挙動を維持）。

設計判断は ADR 0016 に固定する。`HourlyWageStrategy` / `EqualWageStrategy` 側のロジックは変更不要（AllocationPolicy 経由でそのまま伝搬する）。

**Tech Stack:** .NET 10 / C# / xUnit / FluentAssertions

## Global Constraints

- 依存方向: `App → Application → Domain`、`Infrastructure → Application/Domain`。Domain は外部依存なし。本タスクは Domain と Domain.Tests のみ変更（Application.Tests に 1 件追加あり）。
- `record` ＋ append-only、純粋関数（日付/乱数/I/O 非依存）。
- `<Nullable>enable</Nullable>` ＋ `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`。警告ゼロ。
- TDD: Red → Green → Refactor。1 コミット=1 論理変更。コミットメッセージにフェーズ番号 `phase2` と受け入れ基準 ID `AC2-4` を含める。
- 設計判断は ADR で外部化（`docs/decisions/0016-zero-weight-allocation.md`）。報酬告示由来の数値はハードコードしない（本タスクは数値ハードコードなし）。
- 全 RED/GREEN コミットの前に `dotnet format --verify-no-changes` を通過させる（H-1 で学んだ教訓）。
- 公開 API シグネチャ不変: `AllocationPolicy.Allocate(IReadOnlyList<(Guid Key, decimal Weight)>, int, RoundingRule, RemainderPolicy, Guid?) → IReadOnlyList<(Guid Key, int AmountYen)>`。例外契約のみ追加。
- 数値は整数円。中間計算 `decimal`、最終 `Rounding` で整数化。浮動小数点を最終金額に混入させない（既存通り）。

## File Structure

- **新規**: `docs/decisions/0016-zero-weight-allocation.md`
  - ADR 1 ファイル。結論→背景→選択肢→決定→影響の標準フォーマット（既存 ADR と同形式）。
- **修正**: `src/Tsumugi.Domain/Logic/AllocationPolicy.cs:5-71`
  - `Allocate` の `totalWeight <= 0m` 早期 return を分岐実装に置換。公開 API/シグネチャ不変、例外契約を追加。
- **修正**: `tests/Tsumugi.Domain.Tests/AllocationPolicyTests.cs:7-81`
  - `All_zero_weights_yield_zero_amounts` を意味分割し、3 ケースに展開。
  - 不変条件 property test を強化（既存 `Sum_invariant_holds_for_random_weights` はそのまま温存）。
- **修正**: `tests/Tsumugi.Domain.Tests/Wage/HourlyWageStrategyTests.cs:9-57`
  - `All_zero_minutes_yields_all_zero` を意味分割（fund=0 のケース ＋ fund>0 で throw のケース）。
- **追加**: `tests/Tsumugi.Domain.Tests/Wage/EqualWageStrategyTests.cs`（既存ファイルに 2 件追加）
  - 全員 PresentDays=0 + fund>0 で throw のテスト。
  - 全員 PresentDays=0 + fund=0 で全員 0 円のテスト。
- **追加**: `tests/Tsumugi.Application.Tests/CalculateWagesUseCaseTests.cs`（既存ファイルに 1 件追加）
  - Hourly + 全員 0 分 + fund>0 で `InvalidOperationException` が伝搬することを確認（UseCase 契約確認）。

## 不変条件・仕様根拠

- 仕様: `05_ClaudeCode_Phase2実装指示_工賃計算_Tsumugi.md:91`
  > 按分方式（Hourly/Equal）は**配分後合計＝原資**を保証する。切り捨てで生じた余りを `RemainderPolicy` で決定的に配分（既定: 最大剰余法で残円を所定順に1円ずつ／または事業所留保）。**「合計＝原資」を不変条件としてテスト**。
- 仕様: `05_ClaudeCode_Phase2実装指示_工賃計算_Tsumugi.md:122`
  > **配分端数の不変条件「Σ配分＝原資」**（割り切れない比率、利用者1名、全員0分、極端な偏り）。
- 仕様: AC2-4「按分方式で **Σ配分＝原資** が常に成立（端数・余りの決定的処理）」
- 「常に成立」の本計画における解釈: **配分結果が返るときは必ず Σ=原資**。重み総和ゼロかつ原資>0 のように決定論的配分が定義できないケースは、`LargestRemainder` では例外で拒否することで不変条件を vacuously に守る。`ReserveToOffice` では全額を事業所留保し、Σ=原資 を明示的に維持する。

---

### Task 1: ADR 0016 — ゼロ重み時の按分挙動

**Files:**
- Create: `docs/decisions/0016-zero-weight-allocation.md`

**Interfaces:**
- Consumes: 既存 `RemainderPolicy` enum (`LargestRemainder`, `ReserveToOffice`)、既存 ADR 0012「工賃計算の方式戦略・端数・年度起点」
- Produces: Task 2/3 が参照する正式な設計決定文書。例外メッセージ文言の正本もここに置く。

- [ ] **Step 1.1: ADR ファイルを作成**

`docs/decisions/0016-zero-weight-allocation.md` を以下の内容で作成（既存 ADR と同フォーマット、和文・本文も完全に固定）：

```markdown
# ADR 0016: ゼロ重み時の按分挙動

## 結論
- `AllocationPolicy.Allocate(shares, totalYen, rounding, remainder, officeReserveKey)` における重み総和 `totalWeight <= 0` のケースを、`RemainderPolicy` ごとに決定論で分岐させる。
- 分岐定義:
  1. `totalYen == 0` → 全員 0 円を返す（既存挙動を維持）。
  2. `totalYen > 0` ∧ `remainder == RemainderPolicy.ReserveToOffice` → 全額 `totalYen` を `officeReserveKey` に集約。他の `shares` は 0 円。
  3. `totalYen > 0` ∧ `remainder == RemainderPolicy.LargestRemainder` → `InvalidOperationException` を投げる（メッセージは「配分対象の総重みが 0 のため、原資 X 円を最大剰余法で配分できません。事業所留保へ切り替えるか、原資を 0 円に設定してください。」）。
- 上記により AC2-4「按分方式で Σ配分＝原資 が常に成立」を保つ：配分が返る場合は必ず Σ=原資、決定論的配分が定義できないケースは例外で拒否する（vacuously に成立）。

## 背景
- Codex レビュー H-2 で「全員 0 分」入力時に `AllocationPolicy.Allocate` が `Σ配分 = 0 ≠ 原資` を返してしまうことが指摘された。
- 仕様 `05_ClaudeCode_Phase2実装指示_工賃計算_Tsumugi.md:91` は按分方式の **配分後合計＝原資** を不変条件として要求。
- 仕様 `05_ClaudeCode_Phase2実装指示_工賃計算_Tsumugi.md:122` は AC2-4 の試験ケースに「全員 0 分」を明記。
- B 型工賃は労働対価であるため「働かなかった人へ均等に支払う」既定はとらない。一方で `WageFund` が既に正の値で登録されている以上、整合性として Σ=原資 を保つ必要がある。

## 選択肢
1. **重みゼロを等価重みとみなしてフォールバック均等割** — 「全員 0 分でも一律支給」となり、B 型の労働対価原則に反する。出席者ゼロでも資金が消える。却下。
2. **常に事業所留保（policy を無視して office に全額）** — `RemainderPolicy.LargestRemainder` を明示選択しているユーザの意図を無視する。policy が事実上 1 種類に退化する。却下。
3. **policy 別に分岐（採用）** — `ReserveToOffice` 選択時は全額留保（自然な拡張）。`LargestRemainder` 選択時は決定論的順序が定義できないため例外で拒否し、UI から原資修正 or policy 切替を促す。policy の意味論を保持しつつ Σ=原資 不変条件を守れる。
4. **暗黙の `ReserveToOffice` フォールバック** — 仕様にないルールを暗黙導入することになり、設定意図と挙動の乖離を生む。却下。

## 決定
選択肢 3 を採用。`AllocationPolicy.Allocate` の `totalWeight <= 0m` 分岐を上記「結論」の 3 ケースで実装する。例外メッセージは結論セクションに明記した文言で固定する（テストでメッセージパターンを検証）。

## 影響
- `AllocationPolicy.Allocate` の例外契約に `InvalidOperationException`（重み 0 ∧ 原資>0 ∧ LargestRemainder）を追加。呼び出し側（`HourlyWageStrategy`, `EqualWageStrategy`, `CalculateWagesUseCase`）は伝搬のみで追加処理なし。
- UseCase 経由で UI に届く `InvalidOperationException` は既存の「`WageFund` 必須」「`WageSettings` 未登録」と同型で扱える。
- 既存テスト `AllocationPolicyTests.All_zero_weights_yield_zero_amounts` および `HourlyWageStrategyTests.All_zero_minutes_yields_all_zero` は意味分割して書き換える（旧期待は本 ADR で正式に上書き）。
- `EqualWageStrategy` は「出席者ゼロ ∧ 原資>0」で同じ例外を伝搬する。
- 将来 `ReserveToOffice` を既定にしたい事業所が現れた場合は `WageSettings.Remainder` を切り替えるだけで挙動が決まる（ADR 0012 既存スコープで吸収）。
```

- [ ] **Step 1.2: 整形確認とコミット**

Run:
```bash
dotnet format --verify-no-changes
```

Expected: 差分なし（ADR は .md なので影響しないが念のため）。

```bash
git add docs/decisions/0016-zero-weight-allocation.md
git commit -m "docs(phase2): ADR 0016 ゼロ重み時の按分挙動 (AC2-4)"
```

---

### Task 2: RED — AllocationPolicy / Strategy / UseCase に新不変条件テスト追加

**Files:**
- Modify: `tests/Tsumugi.Domain.Tests/AllocationPolicyTests.cs:7-81`
- Modify: `tests/Tsumugi.Domain.Tests/Wage/HourlyWageStrategyTests.cs:9-57`
- Modify: `tests/Tsumugi.Domain.Tests/Wage/EqualWageStrategyTests.cs:9-36`
- Modify: `tests/Tsumugi.Application.Tests/CalculateWagesUseCaseTests.cs:11-227`

**Interfaces:**
- Consumes: 既存 `AllocationPolicy.Allocate`、Domain enums、Strategy 群、`CalculateWagesUseCase`、`WageInputs`、`WageFund`
- Produces: 失敗する RED テスト 7 件。Task 3 で GREEN にする。例外メッセージは ADR 0016 結論セクションの文言と一致する必要がある。

- [ ] **Step 2.1: `tests/Tsumugi.Domain.Tests/AllocationPolicyTests.cs` の既存 `All_zero_weights_yield_zero_amounts` を意味分割して 3 件に置換**

`All_zero_weights_yield_zero_amounts` のメソッド全体（クラス内 `[Fact] public void All_zero_weights_yield_zero_amounts()` ブロック）を以下 3 メソッドで置換：

```csharp
[Fact]
public void Zero_weights_with_zero_total_yields_all_zero()
{
    var r = AllocationPolicy.Allocate(
        new[] { S(1, 0m), S(2, 0m) }, 0,
        RoundingRule.FloorYen, RemainderPolicy.LargestRemainder);
    r.Should().HaveCount(2);
    r.Should().AllSatisfy(t => t.AmountYen.Should().Be(0));
}

[Fact]
public void Zero_weights_with_positive_total_and_largest_remainder_throws()
{
    var act = () => AllocationPolicy.Allocate(
        new[] { S(1, 0m), S(2, 0m) }, 1000,
        RoundingRule.FloorYen, RemainderPolicy.LargestRemainder);
    act.Should().Throw<InvalidOperationException>()
        .WithMessage("配分対象の総重みが 0 のため、原資 1,000 円を最大剰余法で配分できません。事業所留保へ切り替えるか、原資を 0 円に設定してください。");
}

[Fact]
public void Zero_weights_with_positive_total_and_reserve_to_office_dumps_all_to_office()
{
    var officeKey = new Guid("00000000-0000-0000-0000-000099999999");
    var r = AllocationPolicy.Allocate(
        new[] { S(1, 0m), S(2, 0m) }, 1000,
        RoundingRule.FloorYen, RemainderPolicy.ReserveToOffice, officeKey);
    r.Should().Contain(t => t.Key == officeKey && t.AmountYen == 1000);
    r.Where(t => t.Key != officeKey).Sum(t => t.AmountYen).Should().Be(0);
    r.Sum(t => t.AmountYen).Should().Be(1000);
}
```

- [ ] **Step 2.2: `HourlyWageStrategyTests.All_zero_minutes_yields_all_zero` を意味分割**

`All_zero_minutes_yields_all_zero` メソッド全体を以下 2 メソッドで置換：

```csharp
[Fact]
public void All_zero_minutes_with_zero_fund_yields_all_zero()
{
    var a = new WageInputs(Guid.NewGuid(), 0, 0, 0, 0);
    var b = new WageInputs(Guid.NewGuid(), 0, 0, 0, 0);
    var lines = new HourlyWageStrategy().Calculate(new[] { a, b }, Fund(0), Settings());
    lines.Sum(l => l.AmountYen).Should().Be(0);
    lines.Should().AllSatisfy(l => l.AmountYen.Should().Be(0));
}

[Fact]
public void All_zero_minutes_with_positive_fund_throws_to_preserve_sigma_invariant()
{
    var a = new WageInputs(Guid.NewGuid(), 0, 0, 0, 0);
    var b = new WageInputs(Guid.NewGuid(), 0, 0, 0, 0);
    var act = () => new HourlyWageStrategy().Calculate(new[] { a, b }, Fund(100_000), Settings());
    act.Should().Throw<InvalidOperationException>()
        .WithMessage("配分対象の総重みが 0 のため、原資 100,000 円を最大剰余法で配分できません。事業所留保へ切り替えるか、原資を 0 円に設定してください。");
}
```

- [ ] **Step 2.3: `EqualWageStrategyTests` に 2 件追加**

`Splits_equally_among_present_recipients` の直後（クラス閉じ `}` の直前）に挿入：

```csharp
[Fact]
public void All_absent_with_zero_fund_yields_all_zero()
{
    var inputs = new[]
    {
        new WageInputs(Guid.NewGuid(), 0, 0, 0, 0),
        new WageInputs(Guid.NewGuid(), 0, 0, 0, 0),
    };
    var lines = new EqualWageStrategy().Calculate(inputs, Fund(0), Settings());
    lines.Sum(l => l.AmountYen).Should().Be(0);
    lines.Should().AllSatisfy(l => l.AmountYen.Should().Be(0));
}

[Fact]
public void All_absent_with_positive_fund_throws_to_preserve_sigma_invariant()
{
    var inputs = new[]
    {
        new WageInputs(Guid.NewGuid(), 0, 0, 0, 0),
        new WageInputs(Guid.NewGuid(), 0, 0, 0, 0),
    };
    var act = () => new EqualWageStrategy().Calculate(inputs, Fund(50_000), Settings());
    act.Should().Throw<InvalidOperationException>()
        .WithMessage("配分対象の総重みが 0 のため、原資 50,000 円を最大剰余法で配分できません。事業所留保へ切り替えるか、原資を 0 円に設定してください。");
}
```

- [ ] **Step 2.4: `CalculateWagesUseCaseTests` に UseCase 伝搬テストを 1 件追加**

`Recipients_without_contract_are_excluded` の直後（クラス閉じ `}` の直前）に挿入：

```csharp
[Fact]
public async Task Hourly_with_all_zero_minutes_and_positive_fund_throws_to_preserve_sigma_invariant()
{
    var r1 = Rec(Guid.NewGuid());
    var r2 = Rec(Guid.NewGuid());
    var period = new DateRange(new DateOnly(2026, 4, 1), null);

    var u = new CalculateWagesUseCase(
        new FakeDailyRepo(new[] { Present(r1.Id, new DateOnly(2026, 7, 1)), Present(r2.Id, new DateOnly(2026, 7, 1)) }),
        new FakeWorkRepo(new[]
        {
            Work(r1.Id, new DateOnly(2026, 7, 1), 0),
            Work(r2.Id, new DateOnly(2026, 7, 1), 0),
        }),
        new FakeFundRepo(new[] { Fund(100_000) }),
        new FakeSettingsRepo(new[] { Settings(WageMethod.Hourly) }),
        new FakeContractRepo(new[] { ContractFor(r1.Id, period), ContractFor(r2.Id, period) }),
        new FakeRecipientRepo(new[] { r1, r2 }),
        AllStrategies);

    var act = async () => await u.ExecuteAsync(Office, 2026, 7, default);
    await act.Should().ThrowAsync<InvalidOperationException>()
        .WithMessage("配分対象の総重みが 0 のため、原資 100,000 円を最大剰余法で配分できません。事業所留保へ切り替えるか、原資を 0 円に設定してください。");
}
```

- [ ] **Step 2.5: ビルドが通り、対象テストが期待通り赤になることを確認**

Run:
```bash
dotnet build
dotnet format --verify-no-changes
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "FullyQualifiedName~AllocationPolicyTests|FullyQualifiedName~HourlyWageStrategyTests|FullyQualifiedName~EqualWageStrategyTests"
dotnet test tests/Tsumugi.Application.Tests/Tsumugi.Application.Tests.csproj --filter "FullyQualifiedName~CalculateWagesUseCaseTests"
```

Expected:
- `dotnet build` 成功（警告ゼロ）。
- `dotnet format --verify-no-changes` 差分なし。
- 赤 7 件:
  1. `Zero_weights_with_positive_total_and_largest_remainder_throws` — 期待 throw、実際は全 0 返却
  2. `Zero_weights_with_positive_total_and_reserve_to_office_dumps_all_to_office` — 期待 office=1000、実際は全 0
  3. `All_zero_minutes_with_positive_fund_throws_to_preserve_sigma_invariant` — 期待 throw、実際は全 0 返却（Σ ≠ 原資）
  4. `All_absent_with_positive_fund_throws_to_preserve_sigma_invariant` — 同上
  5. `Hourly_with_all_zero_minutes_and_positive_fund_throws_to_preserve_sigma_invariant` — 同上
  6. `Zero_weights_with_zero_total_yields_all_zero` — 現状コードでも PASS の想定だが、もし `Reserve_to_office_dumps_remainder_to_office_key` の隣接テストへの影響で計測ノイズが出たら本ステップで把握する。
  7. （`Splits_equally_among_present_recipients` は不変。`All_absent_with_zero_fund_yields_all_zero` は現状コードで PASS）
- 既存テスト（`Empty_shares_returns_empty`, `Even_split_when_weights_equal`, `Largest_remainder_distributes_leftover_yen`, `Reserve_to_office_dumps_remainder_to_office_key`, `Sum_invariant_holds_for_random_weights`, `Distributes_proportional_to_worked_minutes_and_invariant_holds`, `Single_recipient_takes_full_fund`, `Fund_required_for_hourly`, `Splits_equally_among_present_recipients`, `Missing_settings_throws`, `Hourly_requires_fund`, `Piece_method_returns_per_recipient_amounts`, `Hourly_method_preserves_sigma_invariant`, `Recipients_without_contract_are_excluded`）は PASS のまま。

正味の RED は 5 件（throw 期待 3 件 + ReserveToOffice 全額留保 1 件 + UseCase 伝搬 1 件）。`Zero_weights_with_zero_total_yields_all_zero` は GREEN 想定だが現状コードでも通るため計上は実測値で判断する。

- [ ] **Step 2.6: RED 状態でコミット**

```bash
git add tests/Tsumugi.Domain.Tests/AllocationPolicyTests.cs tests/Tsumugi.Domain.Tests/Wage/HourlyWageStrategyTests.cs tests/Tsumugi.Domain.Tests/Wage/EqualWageStrategyTests.cs tests/Tsumugi.Application.Tests/CalculateWagesUseCaseTests.cs
git commit -m "test(phase2): H-2 RED - AC2-4 zero-weight Σ=原資 invariant tests"
```

---

### Task 3: GREEN — AllocationPolicy のゼロ重み分岐実装

**Files:**
- Modify: `src/Tsumugi.Domain/Logic/AllocationPolicy.cs:5-71`

**Interfaces:**
- Consumes: 既存の `RemainderPolicy` enum、`RoundingRule` enum、`shares` 入力
- Produces: 同シグネチャ `Allocate`。新例外契約（`InvalidOperationException`、メッセージは ADR 0016 結論に固定）。
- 呼び出し側 (`HourlyWageStrategy.Calculate:21`, `EqualWageStrategy.Calculate:21`) は変更不要 — 例外は素通しで伝搬する。

- [ ] **Step 3.1: `AllocationPolicy.Allocate` の `totalWeight <= 0m` 分岐を置換**

`src/Tsumugi.Domain/Logic/AllocationPolicy.cs` の `Allocate` メソッドの早期 return ブロックを以下に差し替える。

差し替え対象（現状）:
```csharp
        var totalWeight = shares.Sum(s => s.Weight);
        if (totalWeight <= 0m)
            return shares.Select(s => (s.Key, 0)).ToArray();
```

新ブロック:
```csharp
        var totalWeight = shares.Sum(s => s.Weight);
        if (totalWeight <= 0m)
        {
            if (totalYen == 0)
                return shares.Select(s => (s.Key, 0)).ToArray();

            if (remainder == RemainderPolicy.ReserveToOffice)
            {
                var result = shares.Select(s => (s.Key, AmountYen: 0)).ToList();
                var officeIndex = result.FindIndex(t => t.Key == officeReserveKey!.Value);
                if (officeIndex < 0)
                    result.Add((officeReserveKey!.Value, totalYen));
                else
                    result[officeIndex] = (result[officeIndex].Key, totalYen);
                return result;
            }

            throw new InvalidOperationException(
                $"配分対象の総重みが 0 のため、原資 {totalYen:N0} 円を最大剰余法で配分できません。事業所留保へ切り替えるか、原資を 0 円に設定してください。");
        }
```

メッセージ内のフォーマット `{totalYen:N0}` は InvariantCulture とは異なる結果になる可能性があるため、念のため **既存 `Reserve_to_office_dumps_remainder_to_office_key` のフォーマット規約に合わせて `:N0`（カンマ区切り）を使用** する。テスト側のメッセージもこの形式（`100,000` `50,000` `1,000`）で記述済み。

注意:
- `if (remainder == RemainderPolicy.ReserveToOffice && officeReserveKey is null)` のガード（メソッド冒頭）は既存通り効くので、`officeReserveKey!.Value` の `!` 利用は安全。
- `result.FindIndex` は `(Guid Key, int AmountYen)` タプル比較ではなく `Key` 比較になるよう lambda で明示している。

- [ ] **Step 3.2: 新規/書き換えテストが全てパスすることを確認**

Run:
```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "FullyQualifiedName~AllocationPolicyTests|FullyQualifiedName~HourlyWageStrategyTests|FullyQualifiedName~EqualWageStrategyTests"
dotnet test tests/Tsumugi.Application.Tests/Tsumugi.Application.Tests.csproj --filter "FullyQualifiedName~CalculateWagesUseCaseTests"
```

Expected:
- AllocationPolicyTests: 8 件 PASS（既存 5 件 + 新 3 件、`All_zero_weights_yield_zero_amounts` は削除済み）
- HourlyWageStrategyTests: 5 件 PASS（既存 3 件 + 新 2 件、`All_zero_minutes_yields_all_zero` は削除済み）
- EqualWageStrategyTests: 3 件 PASS（既存 1 件 + 新 2 件）
- CalculateWagesUseCaseTests: 6 件 PASS（既存 5 件 + 新 1 件）

メッセージマッチが失敗する場合は `:N0` の出力結果（CurrentCulture 依存）が原因の可能性あり。
- Mac/Linux CI: `100,000` 形式（en-US）
- 一部 Windows ja-JP 環境: `100,000`（カンマ・ピリオドは ja-JP でも `100,000`）

両方とも `100,000` になるので問題なし。それでも違いが出た場合は **CultureInfo.InvariantCulture を明示** することを検討（Step 3.3 に進む前にコントローラへエスカレート）。

- [ ] **Step 3.3: Domain / Application 全テスト緑を確認（リグレッション無し）**

Run:
```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj
dotnet test tests/Tsumugi.Application.Tests/Tsumugi.Application.Tests.csproj
```

Expected: いずれも全 PASS。

- [ ] **Step 3.4: ソリューション全体 緑を確認**

Run:
```bash
dotnet test
```

Expected: 全 PASS（既存 430 件付近 + 新規追加分。H-1 完了直後の baseline 430/430 + Task 2 追加分から削除分を差し引いた数）。

もし `EqualWageStrategy` 経由でリグレッションが出たら ADR 0016 の方針逸脱の可能性 → 停止して報告。

- [ ] **Step 3.5: ビルド警告ゼロ、format 緑、Domain coverage を確認**

Run:
```bash
dotnet build -warnaserror
dotnet format --verify-no-changes
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --collect:"XPlat Code Coverage"
```

Expected: build 0 警告、format 差分なし、coverage 計測完了。

Domain coverage の数値は H-1 同様、`AllocationPolicy.cs` 自体が 100% line/branch に到達していれば良い（全体閾値の 95% 到達は H-4 が担う）。`AllocationPolicy.cs` のカバレッジが既存より下がっていないことだけ確認し、全体閾値未達は本タスクでブロッカーとしない（report に明記）。

- [ ] **Step 3.6: GREEN コミット**

```bash
git add src/Tsumugi.Domain/Logic/AllocationPolicy.cs
git commit -m "fix(phase2): H-2 GREEN - enforce Σ配分=原資 invariant on zero-weight allocation (AC2-4)"
```

---

## Self-Review チェック結果

**1. Spec coverage:**
- AC2-4「按分方式で Σ配分＝原資 が常に成立」→ Task 2/3 で完全に検証・実装。決定論的配分が定義できないケースは throw で vacuously に成立させる方針を ADR 0016 で固定。
- 仕様 line 91「按分方式は配分後合計＝原資を保証」→ 新規テスト（ReserveToOffice 全額留保、LargestRemainder throw）で守る。
- 仕様 line 122「全員 0 分」→ HourlyWageStrategyTests / EqualWageStrategyTests / CalculateWagesUseCaseTests に明示的に追加。
- ADR 0012「`RemainderPolicy.LargestRemainder` を暫定既定、`ReserveToOffice` も実装」→ Task 1 ADR 0016 が追補として参照、既存方針と整合。

**2. Placeholder scan:** TBD/TODO/「適切なエラー処理」等なし。全ての例外メッセージは ADR 0016 結論セクションに固定文言として保管。

**3. Type consistency:**
- `AllocationPolicy.Allocate` シグネチャは Task 2 アサート ↔ Task 3 実装で一致（公開 API 不変、例外契約のみ追加）。
- `WageFund.NewRecord(Guid id, Guid officeId, YearMonth month, int totalYen, string? note, string createdBy, DateTimeOffset createdAt)` — テストでは `Fund(int yen)` ヘルパ経由で `note=null` 固定。既存利用と同一。
- `WageSettings.Create(...)` のシグネチャ・引数順は既存テスト (`HourlyWageStrategyTests.Settings()`, `EqualWageStrategyTests.Settings()`) 通り。本タスクで触らない。
- `WageInputs(Guid RecipientId, int PresentDays, int TotalWorkedMinutes, int TotalPieceAmountYen, int TotalPoints)` — Task 2 のテスト引数 `(Guid.NewGuid(), 0, 0, 0, 0)` は H-1 の修正後仕様と整合（PresentDays=0、TotalWorkedMinutes=0）。
- `CalculateWagesUseCaseTests.Work(rid, date, minutes)` ヘルパは 0 分も受理可能（既存 `Hourly_method_preserves_sigma_invariant` で 600/400 を渡しており、0 でも型は通る）。

**4. 影響範囲:**
- 公開 API 不変、Strategy / UseCase 改修不要。
- 例外契約追加 → UI 層は既存の `InvalidOperationException` 受け止め経路で吸収可能（VM は別建てで Task 3 範囲外）。VM 配線は H-3 の責務、本タスクではタッチしない。
- ADR 0016 が ADR 0012 を補完する関係。`docs/decisions/` 既存配置と整合。
