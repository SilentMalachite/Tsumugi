using FluentAssertions;
using Tsumugi.Application.Claim;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests.Claim;

public sealed class ClaimFinalizationSnapshotReaderTests
{
    [Fact]
    public void Write_then_parse_roundtrips_all_21_report_fields()
    {
        var snapshot = SampleSnapshot();
        var bytes = ClaimFinalizationSnapshotWriter.Write(snapshot);
        var parsed = ClaimFinalizationSnapshotReader.Parse(bytes);
        parsed.Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public void Write_produces_identical_bytes_for_identical_input()
    {
        var snapshot = SampleSnapshot();
        var a = ClaimFinalizationSnapshotWriter.Write(snapshot);
        var b = ClaimFinalizationSnapshotWriter.Write(snapshot);
        a.Should().Equal(b);
    }

    [Fact]
    public void Parse_rejects_calculation_kind_payload()
    {
        var payload = """{"schemaVersion":"claim-snapshot-v2","validationCodecId":"claim-snapshot-codec-v2","snapshotKind":"calculation"}"""u8.ToArray();
        var act = () => ClaimFinalizationSnapshotReader.Parse(payload);
        act.Should().Throw<InvalidOperationException>().WithMessage("*finalization*");
    }

    [Fact]
    public void Write_then_parse_roundtrips_with_an_active_intensive_support_episode()
    {
        var snapshot = SampleSnapshot() with
        {
            IntensiveSupportEpisode = new ClaimFinalizationIntensiveSupportEpisodeSnapshot(
                new DateOnly(2026, 4, 1)),
        };
        var bytes = ClaimFinalizationSnapshotWriter.Write(snapshot);
        var parsed = ClaimFinalizationSnapshotReader.Parse(bytes);
        parsed.Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public void Write_then_parse_roundtrips_multiple_daily_records_and_claim_lines()
    {
        var snapshot = SampleSnapshot() with
        {
            DailyRecords =
            [
                new ClaimFinalizationDailyRecordSnapshot(
                    new DateOnly(2026, 5, 1), Attendance.Present, true, TransportKind.Round,
                    null, new TimeOnly(9, 0), new TimeOnly(16, 0), null, false,
                    "医療連携", "体験利用", true, true, true, false),
                new ClaimFinalizationDailyRecordSnapshot(
                    new DateOnly(2026, 5, 2), Attendance.Absent, false, TransportKind.None,
                    "電話にて確認", null, null, 45, true,
                    null, null, false, false, false, true),
            ],
            ClaimLines =
            [
                new ClaimFinalizationClaimLineSnapshot(
                    ClaimDetailLineKind.Basic, "B_BASE_W1_C20_S1", 600, 20, 6720),
                new ClaimFinalizationClaimLineSnapshot(
                    ClaimDetailLineKind.Addition, "MEAL_PROVISION_I", 30, 20, 336),
            ],
        };
        var bytes = ClaimFinalizationSnapshotWriter.Write(snapshot);
        var parsed = ClaimFinalizationSnapshotReader.Parse(bytes);
        parsed.Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public void Write_then_parse_roundtrips_populated_optional_fields()
    {
        var snapshot = SampleSnapshot() with
        {
            Certificate = new ClaimFinalizationCertificateSnapshot(
                "9876543210", "131016", "131017", 9300, "0123456789", "テスト上限管理事業所"),
            ClaimInput = new ClaimFinalizationClaimInputSnapshot(
                "管理結果A", 1000, 500,
                new ServiceMonth(2026, 4), new ServiceMonth(2026, 6), 5, 20),
        };
        var bytes = ClaimFinalizationSnapshotWriter.Write(snapshot);
        var parsed = ClaimFinalizationSnapshotReader.Parse(bytes);
        parsed.Should().BeEquivalentTo(snapshot);
    }

    private static ClaimFinalizationSnapshot SampleSnapshot() => new(
        RecipientId: Guid.Parse("11111111-2222-3333-4444-555555555555"),
        ServiceMonth: new ServiceMonth(2026, 5),
        ClaimMasterVersion: "r6-2026-04",
        CsvSpecificationVersion: "r7-10",
        ReportSpecificationVersion: "r1-10",
        Office: new ClaimFinalizationOfficeSnapshot("0123456789", "テスト事業所", RegionGrade.None,
            "1000001", "東京都千代田区千代田1-1", "03-0000-0000", "代表取締役 山田太郎"),
        Recipient: new ClaimFinalizationRecipientSnapshot("山田太郎", "ヤマダタロウ"),
        Certificate: new ClaimFinalizationCertificateSnapshot("9876543210", "131016", null, 9300, null, null),
        ClaimInput: new ClaimFinalizationClaimInputSnapshot(null, null, null, null, null, null, null),
        DailyRecords: [ new ClaimFinalizationDailyRecordSnapshot(
            new DateOnly(2026, 5, 1), Attendance.Present, true, TransportKind.None, null,
            new TimeOnly(9, 0), new TimeOnly(16, 0), null, false, null, null,
            false, false, false, true) ],
        IntensiveSupportEpisode: null,
        ClaimLines: [new ClaimFinalizationClaimLineSnapshot(ClaimDetailLineKind.Basic, "B_BASE_W1_C20_S1", 600, 20, 6720)],
        BilledDays: 20, TotalUnits: 630, TotalCostYen: 7056, BenefitYen: 6351, BurdenYen: 705);
}
