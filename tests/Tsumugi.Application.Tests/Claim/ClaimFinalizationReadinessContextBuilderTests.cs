using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Xunit;
using Kit = Tsumugi.Application.Tests.UseCases.Claim.ClaimPreparationTestKit;

namespace Tsumugi.Application.Tests.Claim;

/// <summary>
/// 確定 snapshot から readiness の値を組み、指定した版の要件で「項目が入っているか」を判定する
/// （ADR 0041）。確定時点の evidence 検査は再現しない。
/// </summary>
public sealed class ClaimFinalizationReadinessContextBuilderTests
{
    // NOTE(teeth): 値の組み立ては確定前（DB 由来）と確定後（snapshot 由来）で同じ関数を通す。
    // path キーを 2 か所に書くと、片方にパスを足して他方を忘れるドリフトが起きる。
    [Fact]
    public void The_snapshot_supplies_exactly_the_paths_that_readiness_knows()
    {
        var fromSnapshot = ClaimFinalizationReadinessContextBuilder
            .BuildValues(Kit.FinalizationSnapshot()).Keys;

        var fromDatabase = ClaimPreparationContextBuilder
            .BuildOfficeValues(new ClaimReadinessOffice("1000001", "住所", "0300000000", "施設長"))
            .Keys
            .Concat(ClaimPreparationContextBuilder.BuildRecipientValues(
                    ClaimReadinessClaimInput.Absent,
                    ClaimReadinessCertificate.Absent,
                    ClaimReadinessContractedProvider.Absent,
                    ClaimDailyRecordAggregate.Empty,
                    null)
                .Keys);

        fromSnapshot.Should().BeEquivalentTo(fromDatabase);
    }

    [Fact]
    public void Entered_values_are_present_and_absent_values_are_not_applicable()
    {
        var snapshot = SnapshotWith(
            claimInput: new ClaimFinalizationClaimInputSnapshot(
                UpperLimitManagementResult.Result1.ToString(), 1_000, 500, null, null, null, null,
                SpecialVisitSupportBilledCount: 2,
                OffsiteSupportCumulativeDays: null));

        var values = ClaimFinalizationReadinessContextBuilder.BuildValues(snapshot);

        values["Office.PostalCode"].Kind.Should().Be(ClaimPreparationValueKind.Text);
        values["Certificate.MunicipalityNumber"].Kind.Should().Be(ClaimPreparationValueKind.Text);
        values["ClaimInput.UpperLimitManagementResult"].Kind.Should().Be(ClaimPreparationValueKind.Code);
        values["ClaimInput.SpecialVisitSupportBilledCount"].Kind.Should().Be(ClaimPreparationValueKind.Number);
        values["ClaimInput.OffsiteSupportCumulativeDays"].Kind.Should()
            .Be(ClaimPreparationValueKind.NotApplicable, "未入力は NotApplicable");
        values["ContractedProvider.FirstServiceDate"].Kind.Should()
            .Be(ClaimPreparationValueKind.NotApplicable, "契約情報を持たない確定分");
        values["DailyRecord.ServiceStartTime"].Kind.Should().NotBe(ClaimPreparationValueKind.NotApplicable);
    }

    // NOTE(teeth): 算定時間数の未入力を 0 として供給すると、要求条件（自己参照でない）が真でも
    // 通ってしまう fail-open になる。DB 由来経路と同じく null は NotApplicable。
    [Fact]
    public void Unentered_billed_hours_stay_not_applicable()
    {
        var withoutHours = ClaimFinalizationReadinessContextBuilder.BuildValues(SnapshotWith());
        var withHours = ClaimFinalizationReadinessContextBuilder.BuildValues(
            SnapshotWith(specialVisitSupportBilledHours: 2));

        withoutHours["DailyRecord.SpecialVisitSupportBilledHours"].Kind.Should()
            .Be(ClaimPreparationValueKind.NotApplicable);
        withHours["DailyRecord.SpecialVisitSupportBilledHours"].Kind.Should()
            .Be(ClaimPreparationValueKind.Number);
    }

    [Fact]
    public void A_requirement_whose_target_is_absent_is_reported_as_missing()
    {
        var requirement = new ClaimInputRequirement(
            "ContractedProvider.FirstServiceDate",
            ["provider:J121:02:008"],
            new ClaimRequirementCondition.Always(),
            ClaimInputDestination.Certificate);

        var issues = ClaimFinalizationReadinessContextBuilder.Evaluate(SnapshotWith(), [requirement]);

        var issue = issues.Should().ContainSingle().Subject;
        issue.Code.Should().Be(ClaimPreparationIssueCode.MissingRequiredField);
        issue.FieldCode.Should().Be("ContractedProvider.FirstServiceDate");
        issue.RecipientId.Should().Be(Kit.RecipientId);
    }

    [Fact]
    public void A_satisfied_requirement_reports_nothing()
    {
        var requirement = new ClaimInputRequirement(
            "Certificate.MunicipalityNumber",
            ["provider:J121:01:004"],
            new ClaimRequirementCondition.Always(),
            ClaimInputDestination.Certificate);

        ClaimFinalizationReadinessContextBuilder.Evaluate(SnapshotWith(), [requirement])
            .Should().BeEmpty();
    }

    // NOTE(teeth): 範囲の線引き（ADR 0041）。確定 snapshot は evidence を運ばないため、
    // ここが evidence 系の issue を出し始めたら「再現できないものを既定値で埋めた」ことになる。
    [Fact]
    public void The_snapshot_evaluation_never_reports_evidence_issues()
    {
        var requirements = new[]
        {
            new ClaimInputRequirement(
                "ClaimInput.UpperLimitManagementResult",
                ["provider:J121:01:016"],
                new ClaimRequirementCondition.Always(),
                ClaimInputDestination.ClaimInput),
            new ClaimInputRequirement(
                "DailyRecord.RecipientConfirmation",
                ["report:service-performance:daily:016"],
                new ClaimRequirementCondition.Always(),
                ClaimInputDestination.DailyRecord),
        };

        var issues = ClaimFinalizationReadinessContextBuilder.Evaluate(SnapshotWith(), requirements);

        issues.Should().OnlyContain(issue =>
            issue.Code == ClaimPreparationIssueCode.MissingRequiredField
            || issue.Code == ClaimPreparationIssueCode.UnresolvedRequirementCondition);
    }

    private static ClaimFinalizationSnapshot SnapshotWith(
        ClaimFinalizationClaimInputSnapshot? claimInput = null,
        int? specialVisitSupportBilledHours = null)
    {
        var baseline = Kit.FinalizationSnapshot();
        return baseline with
        {
            ClaimInput = claimInput
                ?? new ClaimFinalizationClaimInputSnapshot(null, null, null, null, null, null, null),
            DailyRecords =
            [
                baseline.DailyRecords[0] with
                {
                    SpecialVisitSupportBilledHours = specialVisitSupportBilledHours,
                },
            ],
        };
    }
}
