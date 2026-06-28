# M-1 + L-1: オフライン scan 拡張 ＋ PdfGenerator TimeProvider 注入 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 2 件の独立した中/低優先度修正を 1 計画にまとめて完結させる。

- **M-1**: `OfflineComplianceTests.Tsumugi_assemblies_do_not_reference_network_libraries` の `[InlineData]` に `Tsumugi.Infrastructure.Reporting` を追加し、Reporting アセンブリの「`System.Net.*` 系直接参照」がオフライン CI ゲートに含まれるようにする。
- **L-1**: `WageStatementPdfGenerator` が `DateTime.UtcNow` を直接参照している箇所を `TimeProvider` 注入に置き換え、PDF 出力を決定論にする。

**Architecture:**
- M-1 はテストファイル 1 か所への `[InlineData(...)]` 1 行追加。プロダクション変更なし。Reporting は QuestPDF + Domain/Application 参照のみで `System.Net.*` 直接参照は無いはずなので、追加後も GREEN を維持する想定。
- L-1 は `WageStatementPdfGenerator` の primary constructor に `TimeProvider timeProvider` を追加し、両 PDF メソッド内の `DateTime.UtcNow` を `timeProvider.GetUtcNow().UtcDateTime` に置換。DI は `CompositionRoot` で `TimeProvider.System` を既に登録済み (`src/Tsumugi.App/CompositionRoot.cs:27`) のため、無変更で自動解決される。テストは `new WageStatementPdfGenerator(TimeProvider.System)` に更新し、+1 件「決定論」テストを追加。

**Tech Stack:** .NET 10 / C# / xUnit / FluentAssertions / QuestPDF

## Global Constraints

- 依存方向: 本計画は `tests/Tsumugi.Infrastructure.Tests/` 1 ファイル、`src/Tsumugi.Infrastructure.Reporting/` 1 ファイル、`tests/Tsumugi.Infrastructure.Reporting.Tests/` 2 ファイルのみ変更。Domain/Application/App は触らない。
- 純粋関数原則: `WageStatementPdfGenerator` は注入された `TimeProvider` のみを時刻ソースとして使用（環境依存排除）。
- `<Nullable>enable</Nullable>` + `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` — 0 warnings。
- 公開インターフェース `IWageReportGenerator` のシグネチャは不変（決定論化は generator の実装詳細）。
- DI 変更なし: `CompositionRoot.AddTsumugiServices` は無編集（`TimeProvider.System` 既登録、`WageStatementPdfGenerator` は scoped 自動解決）。
- TDD: 各 task で `dotnet format --verify-no-changes`、`dotnet build -warnaserror`、`dotnet test` を緑にしてからコミット。
- コミットメッセージは `phase2` + `M-1` または `L-1` を含む。

## File Structure

| Task | Files |
|---|---|
| 1 (M-1) | `tests/Tsumugi.Infrastructure.Tests/OfflineComplianceTests.cs:26-29` |
| 2 (L-1) | `src/Tsumugi.Infrastructure.Reporting/WageStatementPdfGenerator.cs:13-105`、`tests/Tsumugi.Infrastructure.Reporting.Tests/WageStatementPdfGeneratorTests.cs`、`tests/Tsumugi.Infrastructure.Reporting.Tests/WagePaymentListPdfGeneratorTests.cs` |

## 仕様根拠

- Codex M-1: "新規 `Tsumugi.Infrastructure.Reporting` が通信API直接参照ゲートに入っていません。P/Invoke/URL検査には含まれますが、`System.Net.Http` などの直接参照検査は Domain/Application/Infrastructure のみです。修正方針: Reporting も直接参照スキャン対象へ追加する。"
- Codex L-1: "PDF出力が `DateTime.UtcNow` を直接使うため、同じ入力でも帳票内容が日付依存になります。"
- ハード制約 (CLAUDE.md §1): "オフライン: `Tsumugi.App` を含む**全プロダクションアセンブリ**で通信API使用を検出・禁止"

---

### Task 1: M-1 — `OfflineComplianceTests` の対象アセンブリに Reporting を追加

**Files:**
- Modify: `tests/Tsumugi.Infrastructure.Tests/OfflineComplianceTests.cs:26-29`

**Interfaces:**
- Consumes: 既存 `Tsumugi_assemblies_do_not_reference_network_libraries(string assemblyName)` の `[Theory] [InlineData(...)]` テーブル
- Produces: 4 件目の `[InlineData("Tsumugi.Infrastructure.Reporting")]`。Reporting も `System.Net.*` / `Microsoft.AspNetCore` / `Grpc` 直接参照を禁止される。

- [ ] **Step 1.1: `[InlineData]` を 1 行追加**

`tests/Tsumugi.Infrastructure.Tests/OfflineComplianceTests.cs` の以下の箇所:

```csharp
    [Theory]
    [InlineData("Tsumugi.Domain")]
    [InlineData("Tsumugi.Application")]
    [InlineData("Tsumugi.Infrastructure")]
    public void Tsumugi_assemblies_do_not_reference_network_libraries(string assemblyName)
```

これを以下に置換（**4 行目に新規 InlineData 追加**）:

```csharp
    [Theory]
    [InlineData("Tsumugi.Domain")]
    [InlineData("Tsumugi.Application")]
    [InlineData("Tsumugi.Infrastructure")]
    [InlineData("Tsumugi.Infrastructure.Reporting")]
    public void Tsumugi_assemblies_do_not_reference_network_libraries(string assemblyName)
```

その他は触らない。

- [ ] **Step 1.2: ビルド・フォーマット・テスト**

```bash
dotnet build
dotnet format --verify-no-changes
dotnet test tests/Tsumugi.Infrastructure.Tests --filter "FullyQualifiedName~OfflineComplianceTests"
```

Expected:
- ビルド 0 warnings、format clean。
- `Tsumugi_assemblies_do_not_reference_network_libraries` が 4 件 PASS（Reporting も含めて）。
- もし Reporting が `System.Net.*` 系を直接参照していて Reporting テストが FAIL する場合、STOP して報告（Reporting の csproj 依存を Codex review で再確認する必要あり）。

- [ ] **Step 1.3: コミット**

```bash
git add tests/Tsumugi.Infrastructure.Tests/OfflineComplianceTests.cs
git commit -m "test(phase2): M-1 add Reporting to network-library reference scan"
```

---

### Task 2: L-1 — `WageStatementPdfGenerator` に `TimeProvider` 注入

**Files:**
- Modify: `src/Tsumugi.Infrastructure.Reporting/WageStatementPdfGenerator.cs:13-105`
- Modify: `tests/Tsumugi.Infrastructure.Reporting.Tests/WageStatementPdfGeneratorTests.cs`
- Modify: `tests/Tsumugi.Infrastructure.Reporting.Tests/WagePaymentListPdfGeneratorTests.cs`

**Interfaces:**
- Consumes: `TimeProvider` (System 名前空間、`Microsoft.Extensions.TimeProvider` ではなく `System.TimeProvider`)、既存 DTO 群、QuestPDF API
- Produces: コンストラクタが `WageStatementPdfGenerator(TimeProvider timeProvider)` 形式に変わる。`IWageReportGenerator` インターフェースは不変。

**事前メモ:**
- `TimeProvider` は .NET 8 標準。`TimeProvider.System` がデフォルト実装。
- `CompositionRoot.AddTsumugiServices` (`src/Tsumugi.App/CompositionRoot.cs:27`) で `services.AddSingleton(TimeProvider.System)` 登録済み → DI 経由で自動注入。
- 既存 PDF テスト 4 件 (`Statement_pdf_contains_recipient_name_office_name_and_amount`, `Generate_rejects_null_arguments`, `PaymentList_includes_each_amount_total_and_recipient_count`, `Empty_statements_yields_zero_total_and_zero_average_no_crash`) は `new WageStatementPdfGenerator()` を呼ぶので、`new WageStatementPdfGenerator(TimeProvider.System)` に書き換える。

- [ ] **Step 2.1: `WageStatementPdfGenerator` を `TimeProvider` 注入に変更**

`src/Tsumugi.Infrastructure.Reporting/WageStatementPdfGenerator.cs` のクラス宣言を:

```csharp
public sealed class WageStatementPdfGenerator : IWageReportGenerator
{
    public byte[] GenerateStatement(...)
```

から以下へ変更:

```csharp
public sealed class WageStatementPdfGenerator(TimeProvider timeProvider) : IWageReportGenerator
{
    public byte[] GenerateStatement(...)
```

両メソッド内の発行日生成箇所 (各メソッドに 1 か所ずつ):

```csharp
                p.Footer().AlignCenter().Text(t =>
                {
                    t.Span("発行日: ");
                    t.Span(DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
                });
```

を以下に置換（**両メソッドとも**）:

```csharp
                p.Footer().AlignCenter().Text(t =>
                {
                    t.Span("発行日: ");
                    t.Span(timeProvider.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
                });
```

ファイル先頭の `using` で `System;` が既存なので追加不要 (`TimeProvider` は `System` namespace)。

- [ ] **Step 2.2: 既存テスト 4 件の `new WageStatementPdfGenerator()` を更新**

両テストファイル内のすべての `new WageStatementPdfGenerator()` 呼び出しを `new WageStatementPdfGenerator(TimeProvider.System)` に置換する。

`tests/Tsumugi.Infrastructure.Reporting.Tests/WageStatementPdfGeneratorTests.cs`:
- 影響箇所: `Statement_pdf_contains_recipient_name_office_name_and_amount` (line 39), `Generate_rejects_null_arguments` (line 54)
- 各箇所 `new WageStatementPdfGenerator()` → `new WageStatementPdfGenerator(TimeProvider.System)`

`tests/Tsumugi.Infrastructure.Reporting.Tests/WagePaymentListPdfGeneratorTests.cs`:
- 影響箇所: `PaymentList_includes_each_amount_total_and_recipient_count` (line 37), `Empty_statements_yields_zero_total_and_zero_average_no_crash` (line 53)
- 同様に置換

- [ ] **Step 2.3: 決定論テスト 1 件追加**

`tests/Tsumugi.Infrastructure.Reporting.Tests/WageStatementPdfGeneratorTests.cs` の末尾（クラス閉じ `}` の直前）に挿入:

```csharp
[Fact]
public void Statement_pdf_is_deterministic_for_same_inputs_and_same_timeprovider()
{
    var fixedTime = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);
    var tp = new FixedTimeProvider(fixedTime);
    var gen = new WageStatementPdfGenerator(tp);

    var stmt = new WageStatementDto(
        Guid.NewGuid(), Guid.NewGuid(), 2026, 7, Guid.NewGuid(),
        AmountYen: 50_000, BasisSummary: "時間割方式: 600分 / 原資100,000円",
        Kind: RecordKind.New, OriginId: null,
        CreatedAt: new DateTimeOffset(2026, 7, 31, 9, 0, 0, TimeSpan.Zero));
    var recipient = new RecipientDto(
        Guid.NewGuid(), "氏名", "シメイ", new DateOnly(1990, 1, 1), ArchivedAt: null,
        ConcurrencyToken: Guid.NewGuid());
    var office = new OfficeDto(
        Guid.NewGuid(), "1234567890", "事業所",
        Tsumugi.Domain.Enums.ServiceCategory.TypeB,
        Tsumugi.Domain.Enums.RegionGrade.None,
        ConcurrencyToken: Guid.NewGuid());

    var a = gen.GenerateStatement(stmt, recipient, office);
    var b = gen.GenerateStatement(stmt, recipient, office);

    a.Should().BeEquivalentTo(b, "同一入力＋同一 TimeProvider なら出力 PDF は決定論的に同一");
}

private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
```

**注意**:
- `WageStatementDto` / `RecipientDto` / `OfficeDto` のコンストラクタシグネチャは既存テストの呼び出し例を参照して合わせる（型推論で named arg 指定）。差異があれば既存ヘルパに合わせて修正。
- `FixedTimeProvider` は test class 内 nested、簡易実装。`GetUtcNow()` のみオーバーライドすれば本テストで十分。

- [ ] **Step 2.4: ビルド・フォーマット・テスト**

```bash
dotnet build
dotnet format --verify-no-changes
dotnet test tests/Tsumugi.Infrastructure.Reporting.Tests
```

Expected:
- ビルド 0 warnings。
- format clean。
- Reporting テスト全 PASS（既存 4 件 + 新規 1 件）。

- [ ] **Step 2.5: ソリューション全体テスト**

`CompositionRoot` の DI 変更なしで `WageStatementPdfGenerator` が `TimeProvider` を自動解決することを App.Tests 経由で確認。

```bash
dotnet test
```

Expected: 全 PASS（前 baseline 462 + 1 新規 = 463、または相当数）。

- [ ] **Step 2.6: コミット**

```bash
git add src/Tsumugi.Infrastructure.Reporting/WageStatementPdfGenerator.cs \
        tests/Tsumugi.Infrastructure.Reporting.Tests/WageStatementPdfGeneratorTests.cs \
        tests/Tsumugi.Infrastructure.Reporting.Tests/WagePaymentListPdfGeneratorTests.cs
git commit -m "refactor(phase2): L-1 inject TimeProvider into WageStatementPdfGenerator for deterministic PDF output"
```

---

## Self-Review チェック結果

**1. Spec coverage:**
- M-1: Codex の修正方針「Reporting も直接参照スキャン対象へ追加」を Step 1.1 で 1 行追加して完全達成。
- L-1: Codex の指摘「PDF 出力が `DateTime.UtcNow` 直接参照」を `TimeProvider` 注入で解消。決定論テストで「同一入力＋同一 provider なら同一バイト列」を不変条件として固定。
- ハード制約 §1 (オフライン): Reporting が CI ゲートに入る。

**2. Placeholder scan:** TBD/TODO 等なし。すべて具体コード提示。

**3. Type consistency:**
- `TimeProvider` (`System.TimeProvider`) は既存 DI 登録 (`CompositionRoot.cs:27`) で `TimeProvider.System` を `AddSingleton` 済み。VM や他の generator と同じ pattern。
- `IWageReportGenerator` のメソッドシグネチャ不変。
- `WageStatementDto`/`RecipientDto`/`OfficeDto` のシグネチャは既存テストの呼び出しから引用。新規テストでも同じスタイルを採用。

**4. 影響範囲:**
- M-1: テスト 1 ファイルのみ、production 不変。
- L-1: production 1 ファイル + テスト 2 ファイル、DI 設定不変、インターフェース不変。

**5. リスク評価:**
- M-1 リスク: もし Reporting が想定外に `System.Net.*` を間接参照していた場合 Step 1.2 で赤になる。その場合は STOP して原因（QuestPDF 経由かも）を調査・対処。
- L-1 リスク: `FixedTimeProvider` の nested test class が他の同様テストと型衝突しないこと（既存に `FixedClock` 等はあるが namespace 異なれば OK）。万一あれば nested を private にして衝突回避済み。
