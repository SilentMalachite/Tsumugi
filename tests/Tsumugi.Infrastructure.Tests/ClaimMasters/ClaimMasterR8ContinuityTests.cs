using System.Text.Json;
using FluentAssertions;

namespace Tsumugi.Infrastructure.Tests.ClaimMasters;

/// <summary>
/// 2026-06（R8-06施行月）に適用される全master entryが、R8適用性を確認済みの出典に
/// 裏付けられているか、または明示的に適用期間を閉じられているかを網羅検査する
/// （ADR 0044・AC3-4-1）。
/// 「出典なしでR8へ到達するentry」＝エラーを出さずに古い値で請求を生成しうる経路を0件に保つ。
/// </summary>
public sealed class ClaimMasterR8ContinuityTests
{
    private const string SeedDirectory = "src/Tsumugi.Infrastructure/ClaimMasters/Seed";

    /// <summary>
    /// 「R8-06に適用されることをプロジェクトとして確認済みの出典」のdocumentId。
    /// R8-06改定資料束7件（basic-rewards / additions / service-codesの継続照合に使う。
    /// ADR 0027決定6・ADR 0028決定1）に加え、地域単価・負担上限のように改定資料束とは
    /// 別の法令・通知系統に属する制度値については、その系統でR8-06時点の適用を直接
    /// 確認した出典を個別に加える（ADR 0044 決定1）。ここに載るdocumentIdをsourceRefsに
    /// 1件以上持つentryだけが、2026-06へ適用期間を開いたまま到達してよい。
    /// </summary>
    private static readonly string[] R8AppliedDocumentIds =
    [
        // --- R8-06 報酬改定資料束（basic-rewards / additions / service-codes向け） ---
        "r8-fee-notice", // 令和8年6月1日施行の改正告示そのもの（R8-06改定の直接一次資料）
        "r8-reward-structure", // 令和8年6月施行分の報酬算定構造（R8-06適用を明示する構造表）
        "r8-service-codes-2-xlsx", // 令和8年6月施行分のサービスコード表（R8-06適用のコード・単位数）
        "r8-service-codes-2-pdf", // 同上のPDF版（xlsxとの独立2方式照合の一方）
        "r8-b-reward-band-guide", // 令和8年6月施行分のB型基本報酬区分見直し資料（R8-06適用範囲を明示）
        "r8-calculation-note", // 令和8年6月1日施行の留意事項通知（R8-06適用の算定条件を収載）
        "r8-capability-202606", // 令和8年6月版の体制状況一覧表（R8-06時点で有効な体制項目を収載）

        // --- 地域区分単価・負担上限額向け（報酬改定資料束とは別の法令・通知系統） ---
        // 注意: mhlw-unit-price-notice-observed-946c3d96（原観測・2026-07-10取得）は
        // region-unit-pricesの全entryが既にauthoritativeで引いているため、これをここに
        // 加えるとentry側のcross-check refを1件も追加しなくてもテストが無条件で通ってしまう
        // （歯が立たない。Fix Round 2で発見）。R8-06適用性を独立に立証する出典は、
        // R8施行後に行った「別の観測イベント」でなければならないため、別documentIdとして
        // 登録したR8後再観測（下記）だけをここに載せ、原観測は載せない。
        "mhlw-unit-price-notice-post-r8-observed-946c3d96", // R8-06施行後（2026-07-26）に独立再取得した観測。retrievedAtがR8施行後である点が、原観測（2026-07-10）と違いR8適用性の直接証拠になる
        "r8-burden-recognition-guide-202606", // 版そのものが令和8年6月版（2026-06-05公開）。表の値がburden-caps.jsonの現行値と完全一致することを確認した
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
    public void Every_entry_reaching_june_2026_is_backed_by_an_applied_source(string fileName)
    {
        var appliedSources = R8AppliedDocumentIds.ToHashSet(StringComparer.Ordinal);
        using var document = OpenSeed(fileName);

        var unbacked = new List<string>();
        foreach (var entry in document.RootElement.GetProperty("entries").EnumerateArray())
        {
            if (!ReachesJune2026(entry))
                continue;

            var backed = entry.GetProperty("sourceRefs").EnumerateArray().Any(
                sourceRef => appliedSources.Contains(sourceRef.GetProperty("documentId").GetString()!));

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
