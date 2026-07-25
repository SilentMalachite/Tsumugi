using FluentAssertions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos.Claim.Reports;
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

    [Fact]
    public void Write_then_parse_roundtrips_group_b_explicit_addition_inputs()
    {
        var snapshot = SampleSnapshot() with
        {
            ClaimInput = new ClaimFinalizationClaimInputSnapshot(
                null, null, null, null, null, null, null,
                SpecialVisitSupportBilledCount: 2,
                OffsiteSupportCumulativeDays: 12),
            DailyRecords =
            [
                new ClaimFinalizationDailyRecordSnapshot(
                    new DateOnly(2026, 5, 1), Attendance.Present, true, TransportKind.None, null,
                    new TimeOnly(9, 0), new TimeOnly(16, 0), 45, false, null, null,
                    false, false, false, true,
                    SpecialVisitSupportBilledHours: 3),
            ],
        };

        var parsed = ClaimFinalizationSnapshotReader.Parse(
            ClaimFinalizationSnapshotWriter.Write(snapshot));

        parsed.Should().BeEquivalentTo(snapshot);
        parsed.ClaimInput.SpecialVisitSupportBilledCount.Should().Be(2);
        parsed.ClaimInput.OffsiteSupportCumulativeDays.Should().Be(12);
        parsed.DailyRecords[0].SpecialVisitSupportBilledHours.Should().Be(3);
    }

    /// <summary>
    /// Phase 3-3 で追加した項目（<c>contractedProvider</c> / <c>serviceUsageDays</c> /
    /// グループB個別入力3項目）は、それより前に確定した snapshot では<b>キー自体が存在しない</b>。
    /// キー不在を parse エラーにすると過去の確定分が一切読めなくなるため、null として復元されること
    /// （＝要否の判定は CSV 生成側の fail-close に委ねること）を固定する。
    /// </summary>
    [Fact]
    public void Parse_reads_a_pre_phase33_snapshot_that_lacks_the_new_properties_as_null()
    {
        var parsed = ClaimFinalizationSnapshotReader.Parse(LegacyCanonicalJson);

        parsed.ContractedProvider.Should().BeNull();
        parsed.ServiceUsageDays.Should().BeNull();
        parsed.ClaimInput.SpecialVisitSupportBilledCount.Should().BeNull();
        parsed.ClaimInput.OffsiteSupportCumulativeDays.Should().BeNull();
        parsed.DailyRecords.Should().ContainSingle()
            .Which.SpecialVisitSupportBilledHours.Should().BeNull();
        // 既存項目は従来どおり読めていること（後方互換の緩和が読み取り全体を壊していない確認）。
        parsed.BilledDays.Should().Be(20);
        parsed.ClaimInput.StandardUsageDayTotal.Should().Be(22);
        parsed.DailyRecords[0].SpecialVisitSupportMinutes.Should().Be(45);
    }

    /// <summary>Phase 3-3 より前の Writer が出力していたキー集合そのままの canonical JSON。</summary>
    private static readonly byte[] LegacyCanonicalJson = """
        {"schemaVersion":"claim-snapshot-v2","validationCodecId":"claim-snapshot-codec-v2",
        "snapshotKind":"finalization","recipientId":"11111111-2222-3333-4444-555555555555",
        "serviceMonth":"2026-05","claimMasterVersion":"r6-2026-04","csvSpecificationVersion":"r7-10",
        "reportSpecificationVersion":"r1-10",
        "office":{"officeNumber":"0123456789","officeName":"テスト事業所","regionGrade":"None",
        "postalCode":"1000001","address":"東京都千代田区千代田1-1","phoneNumber":"03-0000-0000",
        "representativeTitleAndName":"代表取締役 山田太郎"},
        "recipient":{"kanjiName":"山田太郎","kanaName":"ヤマダタロウ"},
        "certificate":{"certificateNumber":"9876543210","municipalityNumber":"131016",
        "subsidyMunicipalityNumber":null,"monthlyCostCap":9300,
        "upperLimitManagementProviderNumber":null,"upperLimitManagementProviderName":null},
        "claimInput":{"upperLimitManagementResult":null,"upperLimitManagedAmountYen":null,
        "municipalSubsidyAmountYen":null,"exceptionalUsageStartMonth":null,
        "exceptionalUsageEndMonth":null,"exceptionalUsageDays":null,"standardUsageDayTotal":22},
        "dailyRecords":[{"serviceDate":"2026-05-01","attendance":"Present","mealProvided":true,
        "transportKind":"None","absenceResponseNote":null,"serviceStartTime":"09:00",
        "serviceEndTime":"16:00","specialVisitSupportMinutes":45,"offsiteSupportApplied":false,
        "medicalCoordinationType":null,"trialUseSupportType":null,
        "regionalCollaborationApplied":false,"intensiveSupportApplied":false,
        "emergencyAdmissionApplied":false,"recipientConfirmation":true}],
        "intensiveSupportEpisode":null,
        "claimLines":[{"kind":"Basic","serviceCode":"B_BASE_W1_C20_S1","unit":600,"count":20,
        "amountYen":6720}],
        "billedDays":20,"totalUnits":630,"totalCostYen":7056,"benefitYen":6351,"burdenYen":705}
        """u8.ToArray();

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
