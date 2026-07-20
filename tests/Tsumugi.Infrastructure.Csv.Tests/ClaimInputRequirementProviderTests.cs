using FluentAssertions;
using Tsumugi.Application.Claim;
using Tsumugi.Infrastructure.Csv.Mapping;

namespace Tsumugi.Infrastructure.Csv.Tests;

public sealed class ClaimInputRequirementProviderTests
{
    [Fact]
    public void Provider_exposes_exact_phase31_target_set()
    {
        var requirements = ClaimInputRequirementProvider.LoadEmbedded().GetRequirements();

        requirements.Select(requirement => requirement.TargetPath).Should()
            .HaveCount(26).And.OnlyHaveUniqueItems();
        requirements.SelectMany(requirement => requirement.FieldIds).Should()
            .HaveCount(51).And.OnlyHaveUniqueItems();
        requirements.Should().OnlyContain(requirement =>
            requirement.Destination != ClaimInputDestination.Unknown);
    }

    [Fact]
    public void Provider_groups_all_artifact_fields_by_typed_target()
    {
        var requirements = ClaimInputRequirementProvider.LoadEmbedded().GetRequirements();

        var municipality = requirements.Single(requirement =>
            requirement.TargetPath == "Certificate.MunicipalityNumber");
        municipality.FieldIds.Should().HaveCount(10);
        municipality.Condition.Should().BeOfType<ClaimRequirementCondition.Always>();
        municipality.Destination.Should().Be(ClaimInputDestination.Certificate);

        var specialVisit = requirements.Single(requirement =>
            requirement.TargetPath == "DailyRecord.SpecialVisitSupportMinutes");
        specialVisit.FieldIds.Should().HaveCount(3);
        specialVisit.Condition.Should().BeOfType<ClaimRequirementCondition.Any>();
        specialVisit.Destination.Should().Be(ClaimInputDestination.DailyRecord);
    }

    [Fact]
    public void Provider_parses_closed_condition_tree_without_exposing_dsl()
    {
        var requirements = ClaimInputRequirementProvider.LoadEmbedded().GetRequirements();
        var medicalCoordination = requirements.Single(requirement =>
            requirement.TargetPath == "DailyRecord.MedicalCoordinationType");

        var any = medicalCoordination.Condition.Should()
            .BeOfType<ClaimRequirementCondition.Any>().Subject;
        var modelIn = any.Conditions.OfType<ClaimRequirementCondition.ModelIn>().Single();
        modelIn.ModelPath.Should().Be("DailyRecord.MedicalCoordinationType");
        modelIn.AllowedValues.Should().Equal(
            "TypeI", "TypeII", "TypeIII", "TypeIV", "TypeV", "TypeVI");

        var all = any.Conditions.OfType<ClaimRequirementCondition.All>().Single();
        all.Conditions.OfType<ClaimRequirementCondition.RowPresent>()
            .Should().ContainSingle(row => row.RowScope == "service-performance.daily");
    }

    [Theory]
    [InlineData("report:service-performance:daily:004", "DailyRecord.ServiceStartTime")]
    [InlineData("report:service-performance:daily:005", "DailyRecord.ServiceEndTime")]
    [InlineData("report:service-performance:daily:016", "DailyRecord.RecipientConfirmation")]
    public void Provider_registers_daily_record_fields_required_on_present_days(
        string fieldId, string targetPath)
    {
        // Task 4: 対象月にAttendance.Presentの日があるとき欠落を許さない3フィールド
        // （ServiceStartTime/ServiceEndTime/RecipientConfirmation）が、report-field-mapping-r8-06.json
        // からClaimInputRequirementとして登録され、DailyRecordViewへ向くことを検証する。
        var requirements = ClaimInputRequirementProvider.LoadEmbedded().GetRequirements();

        var requirement = requirements.Should().ContainSingle(r => r.TargetPath == targetPath).Subject;
        requirement.FieldIds.Should().Contain(fieldId);
        requirement.Destination.Should().Be(ClaimInputDestination.DailyRecord);
    }

    [Fact]
    public void Provider_rejects_unknown_condition()
    {
        var action = () => ClaimInputRequirementProvider.Create(
        [
            Source("field:001", "Target.Value", "unknown(Target.Value)", "ClaimInputView"),
        ]);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*condition*");
    }

    [Fact]
    public void Provider_rejects_conflicting_destination_for_same_target()
    {
        var action = () => ClaimInputRequirementProvider.Create(
        [
            Source("field:001", "Target.Value", "always", "ClaimInputView"),
            Source("field:002", "Target.Value", "always", "CertificateView"),
        ]);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*destination*");
    }

    [Fact]
    public void Provider_rejects_missing_ui_destination()
    {
        var action = () => ClaimInputRequirementProvider.Create(
        [
            Source("field:001", "Target.Value", "always", string.Empty),
        ]);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*destination*");
    }

    private static ClaimInputRequirementSource Source(
        string fieldId,
        string targetPath,
        string condition,
        string destination) =>
        new(fieldId, targetPath, condition, destination);
}
