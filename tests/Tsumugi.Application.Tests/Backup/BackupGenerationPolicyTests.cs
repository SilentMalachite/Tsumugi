using System;
using System.Linq;
using FluentAssertions;
using Tsumugi.Application.Backup;
using Xunit;

namespace Tsumugi.Application.Tests.Backup;

/// <summary>
/// spec 決定4: 同日は最新1つだけ残し、直近7日分を保持する。
/// 「最新N件を残す」にすると、1日に何度も起動終了した日でN件が埋まり
/// 1週間分の履歴が1日分に潰れるため、日付単位で数える。
/// </summary>
public sealed class BackupGenerationPolicyTests
{
    private static readonly DateOnly AsOf = new(2026, 8, 16);

    [Fact]
    public void Keeps_only_the_newest_file_of_each_day()
    {
        string[] files =
        [
            "tsumugi-backup-20260816-090000.db",
            "tsumugi-backup-20260816-180000.db",
            "tsumugi-backup-20260816-120000.db",
        ];

        var deleted = BackupGenerationPolicy.SelectForDeletion(files, AsOf);

        deleted.Should().BeEquivalentTo(
        [
            "tsumugi-backup-20260816-090000.db",
            "tsumugi-backup-20260816-120000.db",
        ]);
    }

    [Fact]
    public void Keeps_seven_days_and_deletes_the_eighth()
    {
        // 2026-08-16 から遡って 8 日分。最も古い 2026-08-09 が落ちる。
        var files = Enumerable.Range(0, 8)
            .Select(i => $"tsumugi-backup-{AsOf.AddDays(-i):yyyyMMdd}-100000.db")
            .ToArray();

        var deleted = BackupGenerationPolicy.SelectForDeletion(files, AsOf);

        deleted.Should().ContainSingle().Which.Should().Be("tsumugi-backup-20260809-100000.db");
    }

    [Fact]
    public void Counts_days_not_files()
    {
        // 同じ日に 10 件あっても「1 日分」として数える。7 日分は残る。
        var manySameDay = Enumerable.Range(0, 10)
            .Select(i => $"tsumugi-backup-20260816-{i:00}0000.db");
        var otherDays = Enumerable.Range(1, 6)
            .Select(i => $"tsumugi-backup-{AsOf.AddDays(-i):yyyyMMdd}-100000.db");

        var deleted = BackupGenerationPolicy.SelectForDeletion(
            manySameDay.Concat(otherDays), AsOf);

        // 削除されるのは 2026-08-16 の古い 9 件だけ。他日の 6 件は 7 日以内なので残る。
        deleted.Should().HaveCount(9);
        deleted.Should().OnlyContain(f => f.StartsWith("tsumugi-backup-20260816-", StringComparison.Ordinal));
    }

    [Fact]
    public void Ignores_pre_restore_snapshots()
    {
        string[] files =
        [
            "pre-restore-20200101-000000.db",
            "pre-restore-20200102-000000.db",
            "tsumugi-backup-20260816-100000.db",
        ];

        var deleted = BackupGenerationPolicy.SelectForDeletion(files, AsOf);

        deleted.Should().BeEmpty();
    }

    [Fact]
    public void Ignores_files_that_do_not_match_the_naming_convention()
    {
        string[] files =
        [
            "readme.txt",
            "tsumugi.db",
            "tsumugi-backup-not-a-date.db",
            "tsumugi-backup-20200101-000000.db",   // 命名規則に合致する古いもの → 削除対象
        ];

        var deleted = BackupGenerationPolicy.SelectForDeletion(files, AsOf);

        deleted.Should().ContainSingle().Which.Should().Be("tsumugi-backup-20200101-000000.db");
    }

    [Fact]
    public void Returns_empty_for_an_empty_input()
    {
        BackupGenerationPolicy.SelectForDeletion([], AsOf).Should().BeEmpty();
    }

    [Fact]
    public void Is_deterministic_and_does_not_depend_on_input_order()
    {
        string[] ascending =
        [
            "tsumugi-backup-20260810-100000.db",
            "tsumugi-backup-20260816-100000.db",
        ];
        var descending = ascending.Reverse().ToArray();

        BackupGenerationPolicy.SelectForDeletion(ascending, AsOf)
            .Should().BeEquivalentTo(BackupGenerationPolicy.SelectForDeletion(descending, AsOf));
    }

    [Fact]
    public void Does_not_delete_files_dated_after_the_reference_day()
    {
        // 時計のずれ・タイムゾーン差で未来日付のファイルが混ざりうる。即座に削除対象にしない。
        string[] files =
        [
            "tsumugi-backup-20260820-100000.db",   // asOf より未来
            "tsumugi-backup-20260816-100000.db",
        ];

        var deleted = BackupGenerationPolicy.SelectForDeletion(files, AsOf);

        deleted.Should().BeEmpty();
    }

    [Fact]
    public void Create_normalises_to_utc_so_the_round_trip_preserves_the_instant()
    {
        // +09:00 の 08-16 01:00 は UTC では 08-15 16:00。ファイル名は UTC 側でなければ
        // SelectForDeletion の日付バケットが1日ずれ、7日境界の判定を誤る。
        var at = new DateTimeOffset(2026, 8, 16, 1, 0, 0, TimeSpan.FromHours(9));

        var name = BackupFileName.Create(at);
        name.Should().Be("tsumugi-backup-20260815-160000.db");

        BackupFileName.TryParse(name, out var parsed).Should().BeTrue();
        parsed.Should().Be(at);          // 同じ瞬間を指すこと
        parsed.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Create_and_TryParse_round_trip()
    {
        var at = new DateTimeOffset(2026, 8, 16, 13, 45, 7, TimeSpan.Zero);

        var name = BackupFileName.Create(at);
        name.Should().Be("tsumugi-backup-20260816-134507.db");

        BackupFileName.TryParse(name, out var parsed).Should().BeTrue();
        parsed.Should().Be(new DateTimeOffset(2026, 8, 16, 13, 45, 7, TimeSpan.Zero));
    }

    [Fact]
    public void TryParse_rejects_pre_restore_names()
    {
        var name = BackupFileName.CreatePreRestore(
            new DateTimeOffset(2026, 8, 16, 13, 45, 7, TimeSpan.Zero));
        name.Should().Be("pre-restore-20260816-134507.db");

        BackupFileName.TryParse(name, out _).Should().BeFalse();
    }
}
