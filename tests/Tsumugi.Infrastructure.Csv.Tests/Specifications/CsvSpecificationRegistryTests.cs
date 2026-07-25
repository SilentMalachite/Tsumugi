using FluentAssertions;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Csv.Specifications;

namespace Tsumugi.Infrastructure.Csv.Tests.Specifications;

/// <summary>
/// CSV 仕様の版レジストリ。施行分ごとの仕様を並存させ、<b>処理対象年月</b>で適用版を選ぶ（ADR 0039）。
/// </summary>
public sealed class CsvSpecificationRegistryTests
{
    private static readonly CsvSpecSourceRef AnyRef = new(
        "interface-index-r7-10",
        new string('a', 64),
        "section=索引",
        "authoritative",
        ["applicability-period"]);

    [Fact]
    public void The_embedded_registry_resolves_the_current_version()
    {
        var registry = CsvSpecificationRegistry.LoadEmbedded();

        registry.Versions.Should().NotBeEmpty();
        registry.Current.Should().Be(registry.Versions[^1].Version);
        registry.Resolve(new ProcessingMonth(2026, 8)).Version.Should().Be(registry.Current);
    }

    // NOTE(teeth): 適用開始前の処理対象年月では、推測で現行版を使わず fail-close する。
    [Fact]
    public void A_processing_month_before_the_first_version_fails_closed()
    {
        var registry = CsvSpecificationRegistry.LoadEmbedded();
        var firstFrom = registry.Versions[0].EffectiveFromProcessingMonth;

        var act = () => registry.Resolve(new ProcessingMonth(2025, 9));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*CSV仕様版が登録されていません*")
            .Which.Message.Should().Contain(firstFrom);
    }

    // NOTE(teeth): Current は「登録済みの最新版」ではなく「その時点で適用される版」。
    // 次の施行分を事前登録したとき、Current が将来版を返すと確定時の記録と出力時の解決が
    // 必ず食い違い、全件が版不一致になる。
    [Fact]
    public void Current_is_the_version_in_force_now_not_the_newest_registered()
    {
        var entries = new[]
        {
            Entry("r7-10", "2025-10", "2027-03"),
            Entry("r9-04", "2027-04", null),
        };

        // 2026-08 時点では r9-04 は事前登録済みだが適用開始前。
        CsvSpecificationRegistry.ResolveVersion(entries, new ProcessingMonth(2026, 8))
            .Should().Be("r7-10");
        CsvSpecificationRegistry.ResolveVersion(entries, new ProcessingMonth(2027, 4))
            .Should().Be("r9-04");
    }

    [Fact]
    public void Current_uses_the_clock_and_fails_closed_when_no_version_applies()
    {
        var registry = CsvSpecificationRegistry.LoadEmbedded(
            new FixedClock(new DateTimeOffset(2020, 1, 15, 9, 0, 0, TimeSpan.FromHours(9))));

        var act = () => registry.Current;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*端末の日付*");
    }

    [Fact]
    public void Current_resolves_the_embedded_version_for_a_date_inside_its_period()
    {
        var registry = CsvSpecificationRegistry.LoadEmbedded(
            new FixedClock(new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.FromHours(9))));

        registry.Current.Should().Be("r7-10");
    }

    [Fact]
    public void Every_registered_version_cites_its_applicability_period()
    {
        var registry = CsvSpecificationRegistry.LoadEmbedded();

        registry.Versions.Should().OnlyContain(
            entry => entry.SourceRefs.Count > 0 && entry.ApplicabilityNote.Length > 0);
        registry.Versions.Should().OnlyContain(
            entry => entry.SourceRefs.All(item => item.Supports.Contains("applicability-period")));
    }

    [Theory]
    [InlineData(2025, 10, "r7-10")]
    [InlineData(2026, 5, "r7-10")]
    [InlineData(2026, 6, "r9-04")]
    [InlineData(2030, 1, "r9-04")]
    public void Two_versions_coexist_and_the_processing_month_picks_one(int year, int month, string expected)
    {
        // 次の施行分が出たときの形（差し替えではなく追記）。r7-10 に effectiveTo を入れ、新版を足す。
        var entries = new[]
        {
            Entry("r7-10", "2025-10", "2026-05"),
            Entry("r9-04", "2026-06", null),
        };

        CsvSpecificationRegistry.ResolveVersion(entries, new ProcessingMonth(year, month))
            .Should().Be(expected);
    }

    [Fact]
    public void Overlapping_periods_are_rejected()
    {
        var file = File(Entry("r7-10", "2025-10", "2026-06"), Entry("r9-04", "2026-06", null));

        var act = () => CsvSpecificationRegistry.ValidateEntries(file);

        act.Should().Throw<InvalidDataException>().WithMessage("*contiguous with no overlap*");
    }

    [Fact]
    public void Gaps_between_periods_are_rejected()
    {
        var file = File(Entry("r7-10", "2025-10", "2026-05"), Entry("r9-04", "2026-08", null));

        var act = () => CsvSpecificationRegistry.ValidateEntries(file);

        act.Should().Throw<InvalidDataException>().WithMessage("*contiguous with no overlap*");
    }

    [Fact]
    public void Only_the_newest_version_may_be_open_ended()
    {
        var file = File(Entry("r7-10", "2025-10", null), Entry("r9-04", "2026-06", null));

        var act = () => CsvSpecificationRegistry.ValidateEntries(file);

        act.Should().Throw<InvalidDataException>().WithMessage("*open ended*");
    }

    [Fact]
    public void The_newest_version_must_be_open_ended()
    {
        var file = File(Entry("r7-10", "2025-10", "2026-05"), Entry("r9-04", "2026-06", "2027-03"));

        var act = () => CsvSpecificationRegistry.ValidateEntries(file);

        act.Should().Throw<InvalidDataException>().WithMessage("*open ended*");
    }

    [Fact]
    public void A_period_that_ends_before_it_begins_is_rejected()
    {
        var file = File(Entry("r7-10", "2026-05", "2025-10"));

        var act = () => CsvSpecificationRegistry.ValidateEntries(file);

        act.Should().Throw<InvalidDataException>().WithMessage("*ends before it begins*");
    }

    [Fact]
    public void A_malformed_period_is_rejected()
    {
        var file = File(Entry("r7-10", "2025/10", null));

        var act = () => CsvSpecificationRegistry.ValidateEntries(file);

        act.Should().Throw<InvalidDataException>().WithMessage("*yyyy-MM*");
    }

    [Fact]
    public void A_version_without_evidence_is_rejected()
    {
        var file = new CsvSpecificationVersionFile(
            1,
            "csv-specification-versions",
            [new CsvSpecificationVersionEntry("r7-10", "令和7年10月施行分", "2025-10", null, [], "note")]);

        var act = () => CsvSpecificationRegistry.ValidateEntries(file);

        act.Should().Throw<InvalidDataException>().WithMessage("*must cite its applicability period*");
    }

    [Fact]
    public void An_empty_registry_is_rejected()
    {
        var act = () => CsvSpecificationRegistry.ValidateEntries(
            new CsvSpecificationVersionFile(1, "csv-specification-versions", []));

        act.Should().Throw<InvalidDataException>().WithMessage("*empty*");
    }

    /// <summary>暦月の判定なので local 時刻を返す固定 clock（月初の境界を JST で評価する）。</summary>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone =>
            TimeZoneInfo.CreateCustomTimeZone("test-jst", TimeSpan.FromHours(9), "test-jst", "test-jst");
    }

    private static CsvSpecificationVersionEntry Entry(string version, string from, string? to) =>
        new(version, $"{version} 施行分", from, to, [AnyRef], "テスト用の適用期間");

    private static CsvSpecificationVersionFile File(params CsvSpecificationVersionEntry[] entries) =>
        new(1, "csv-specification-versions", entries);
}
