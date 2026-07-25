using System.Text.Json;
using FluentAssertions;
using Tsumugi.Infrastructure.ClaimMasters;

namespace Tsumugi.Infrastructure.Tests.ClaimMasters;

/// <summary>
/// 2026-06（R8-06施行月）に適用される全master entryが、R8出典に裏付けられているか、
/// または明示的に適用期間を閉じられているかを網羅検査する（ADR 0044・AC3-4-1）。
/// 「出典なしでR8へ到達するentry」＝エラーを出さずに古い値で請求を生成しうる経路を0件に保つ。
/// </summary>
public sealed class ClaimMasterR8ContinuityTests
{
    private const string SeedDirectory = "src/Tsumugi.Infrastructure/ClaimMasters/Seed";

    /// <summary>
    /// R8-06施行分を裏づける一次資料のdocumentId。ここに載るdocumentIdをsourceRefsに
    /// 1件以上持つentryだけが、2026-06へ適用期間を開いたまま到達してよい。
    /// </summary>
    private static readonly string[] R8AuthoritativeDocumentIds =
    [
        "r8-fee-notice",
        "r8-reward-structure",
        "r8-service-codes-2-xlsx",
        "r8-service-codes-2-pdf",
        "r8-b-reward-band-guide",
        "r8-calculation-note",
        "r8-capability-202606",
    ];

    // spec §3.1 AC3-4-1 の対象はこの2ファイルに限る。basic-rewards / additions /
    // service-codes のR8継続はADR 0027決定6・ADR 0028決定1が照合済みとして記録し、
    // ClaimMasterR8BoundaryTests.Exempt_offices_resolve_the_same_code_and_units_across_the_boundary
    // がruntimeで固定しているため、本テストの対象に含めない。
    // 既存テスト（ClaimCalculatorGoldenCaseTests.GoldenCases）と同じ構築様式に揃える。
    public static TheoryData<string> ValueBearingSeedFiles()
    {
        var data = new TheoryData<string>();
        data.Add("region-unit-prices.json");
        data.Add("burden-caps.json");
        return data;
    }

    [Theory]
    [MemberData(nameof(ValueBearingSeedFiles))]
    public void Every_entry_reaching_june_2026_is_backed_by_an_r8_source(string fileName)
    {
        var r8Sources = R8AuthoritativeDocumentIds.ToHashSet(StringComparer.Ordinal);
        using var document = OpenSeed(fileName);

        var unbacked = new List<string>();
        foreach (var entry in document.RootElement.GetProperty("entries").EnumerateArray())
        {
            if (!ReachesJune2026(entry))
                continue;

            var backed = entry.GetProperty("sourceRefs").EnumerateArray().Any(
                sourceRef => r8Sources.Contains(sourceRef.GetProperty("documentId").GetString()!));

            if (!backed)
                unbacked.Add(entry.GetProperty("key").GetString()!);
        }

        unbacked.Should().BeEmpty(
            "2026-06へ到達するentryはR8出典を持つか、effectiveToで適用期間を閉じなければならない"
            + "（出典なしの継続は、エラーを出さずに古い値で請求を生成する唯一の経路）");
    }

    /// <summary>effectiveFrom ≦ 2026-06 かつ（effectiveTo が null または ≧ 2026-06）。</summary>
    private static bool ReachesJune2026(JsonElement entry)
    {
        const string June2026 = "2026-06";
        var from = entry.GetProperty("effectiveFrom").GetString()!;
        if (string.CompareOrdinal(from, June2026) > 0)
            return false;

        var to = entry.GetProperty("effectiveTo");
        return to.ValueKind == JsonValueKind.Null
            || string.CompareOrdinal(to.GetString()!, June2026) >= 0;
    }

    private static JsonDocument OpenSeed(string fileName)
    {
        var path = Path.Combine(RepositoryRoot(), SeedDirectory, fileName);
        File.Exists(path).Should().BeTrue($"seedファイル {fileName} が存在しなければならない");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tsumugi.sln")))
            directory = directory.Parent;

        directory.Should().NotBeNull("リポジトリルート（Tsumugi.slnのある階層）が見つからなければならない");
        return directory!.FullName;
    }
}
