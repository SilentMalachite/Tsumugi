using System.Text;
using System.Text.Json;
using FluentAssertions;
using Tsumugi.Application.Claim;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests.Claim;

/// <summary>
/// 確定時に凍結する入力snapshotのcanonical bytesを固定する（ADR 0026）。
/// 施設区分は処遇改善加算の率を分ける算定条件（ADR 0047: (Ⅰ)イ は通常0.105・施設0.116）であり、
/// 凍結しないと確定済み請求からどちらの区分で算定したかを復元できない。
/// </summary>
public sealed class ClaimRecipientSnapshotWriterTests
{
    private static readonly ServiceMonth June2026 = new(2026, 6);
    private static readonly Guid RecipientId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static ClaimCalculationRequest RequestWith(string? facilityClassification) => new(
        June2026,
        new ClaimBillingConditionContext(
            RewardSystem: "employment-continuation-support-b",
            PaymentBand: "",
            CapacityHeadcount: 20,
            StaffingKey: "staff-7.5-1",
            AverageWageBandOption: new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 5),
            R8ReformStatus: R8ReformStatus.ReformExempt,
            OfficeCapabilityKeys: new HashSet<string>(StringComparer.Ordinal),
            FacilityClassification: facilityClassification),
        RegionKey: "region-grade-2",
        ServiceKind: "employment-continuation-support",
        Recipients: [Source()]);

    private static RecipientClaimSource Source() => new(
        RecipientId,
        BilledDays: 22,
        BenefitRatePercent: 90,
        CertificateMonthlyCapYen: 9_999_999,
        BurdenCategory: "cat-a");

    private static string Write(string? facilityClassification) => Encoding.UTF8.GetString(
        ClaimRecipientSnapshotWriter.WriteInputSnapshot(
            June2026, RequestWith(facilityClassification), Source(), claimInput: null));

    /// <summary>
    /// 施設区分は conditions の末尾へ記録される（既存キーの順序は変えない）。
    /// </summary>
    [Fact]
    public void The_input_snapshot_records_the_facility_classification_in_conditions()
    {
        using var document = JsonDocument.Parse(Write("designated-support-facility"));

        var conditions = document.RootElement.GetProperty("conditions");
        conditions.GetProperty("facilityClassification").GetString()
            .Should().Be(
                "designated-support-facility",
                "確定済み請求からどちらの施設区分で算定したかを復元できなければならない（ADR 0026・0047）");

        conditions.EnumerateObject().Select(property => property.Name).Should().Equal(
            "rewardSystem",
            "paymentBand",
            "capacityHeadcount",
            "staffingKey",
            "averageWageBandOptionKind",
            "averageWageBandOptionCode",
            "r8ReformStatus",
            "regionKey",
            "serviceKind",
            "facilityClassification");
    }

    /// <summary>
    /// 未入力は推測でgeneralへ落とさず、nullのまま凍結する。
    /// </summary>
    [Fact]
    public void The_input_snapshot_keeps_an_unset_facility_classification_as_null()
    {
        using var document = JsonDocument.Parse(Write(facilityClassification: null));

        document.RootElement.GetProperty("conditions")
            .GetProperty("facilityClassification").ValueKind
            .Should().Be(
                JsonValueKind.Null,
                "未入力を通常事業所として記録すると、施設の過少請求を証跡上も見逃す");
    }

    /// <summary>
    /// 施設区分が違えば canonical bytes も違う。同じ bytes なら PreviewHash も同じになり、
    /// プレビュー後に施設区分を差し替えても同じ hash で確定できてしまう。
    /// </summary>
    [Fact]
    public void The_canonical_bytes_differ_between_facility_classifications()
    {
        var general = ClaimRecipientSnapshotWriter.WriteInputSnapshot(
            June2026, RequestWith("general"), Source(), claimInput: null);
        var facility = ClaimRecipientSnapshotWriter.WriteInputSnapshot(
            June2026, RequestWith("designated-support-facility"), Source(), claimInput: null);
        var unset = ClaimRecipientSnapshotWriter.WriteInputSnapshot(
            June2026, RequestWith(facilityClassification: null), Source(), claimInput: null);

        facility.Should().NotEqual(general, "施設区分の違いは凍結bytesに現れなければならない");
        unset.Should().NotEqual(general, "未入力と非施設は区別されなければならない");
        unset.Should().NotEqual(facility, "未入力と施設は区別されなければならない");
    }

    /// <summary>同一入力は同一bytes（PreviewHash契約の土台）。</summary>
    [Fact]
    public void The_canonical_bytes_are_deterministic_for_the_same_input()
    {
        var first = ClaimRecipientSnapshotWriter.WriteInputSnapshot(
            June2026, RequestWith("designated-support-facility"), Source(), claimInput: null);
        var second = ClaimRecipientSnapshotWriter.WriteInputSnapshot(
            June2026, RequestWith("designated-support-facility"), Source(), claimInput: null);

        first.Should().Equal(second);
    }
}
