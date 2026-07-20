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

    [Theory]
    [InlineData(
        "report:benefit-claim-detail:header:001", "Certificate.MunicipalityNumber",
        ClaimInputDestination.Certificate)]
    [InlineData(
        "report:benefit-claim-detail:header:003", "Certificate.SubsidyMunicipalityNumber",
        ClaimInputDestination.Certificate)]
    [InlineData(
        "report:benefit-claim-detail:upper-limit-management:001",
        "Certificate.UpperLimitManagementProviderNumber", ClaimInputDestination.Certificate)]
    public void Provider_registers_certificate_report_fields(
        string fieldId, string targetPath, ClaimInputDestination expectedDestination)
    {
        // Phase 3-2 Task 5: report-field-mapping-r8-06.jsonのCertificate 3フィールド
        // （header:001/003, upper-limit-management:001）がClaimInputRequirementとして登録され、
        // CertificateViewへ向くことを検証する（Task 4と同型のregistration pin）。
        var requirements = ClaimInputRequirementProvider.LoadEmbedded().GetRequirements();

        var requirement = requirements.Should().ContainSingle(r => r.TargetPath == targetPath).Subject;
        requirement.FieldIds.Should().Contain(fieldId);
        requirement.Destination.Should().Be(expectedDestination);
    }

    [Fact]
    public void Provider_keeps_municipality_number_always_required()
    {
        // Phase 3-2 Task 5: MunicipalityNumberは常時必須（spec §10）。自己参照条件と違い、
        // Always条件は値そのものを見ずにIsPresentだけで判定するため、未入力を検知できる
        // （silently inertにならないことはClaimPreviewProductionWiringTestsのend-to-endで検証）。
        var requirements = ClaimInputRequirementProvider.LoadEmbedded().GetRequirements();

        var municipality = requirements.Single(r => r.TargetPath == "Certificate.MunicipalityNumber");
        municipality.Condition.Should().BeOfType<ClaimRequirementCondition.Always>();
    }

    [Theory]
    [InlineData("Certificate.SubsidyMunicipalityNumber")]
    [InlineData("Certificate.UpperLimitManagementProviderNumber")]
    public void Provider_keeps_optional_certificate_fields_self_referential(string targetPath)
    {
        // Phase 3-2 Task 5: SubsidyMunicipalityNumber / UpperLimitManagementProviderNumberは
        // spec §10でoptional（null許容）。自己参照ModelPresent(<自身>)条件はfail-openのまま
        // 維持する（deliberately weak、ClaimPreparationContextBuilder.csのTask 5 review comment参照）。
        var requirements = ClaimInputRequirementProvider.LoadEmbedded().GetRequirements();

        var requirement = requirements.Single(r => r.TargetPath == targetPath);
        var modelPresent = requirement.Condition.Should()
            .BeOfType<ClaimRequirementCondition.ModelPresent>().Subject;
        modelPresent.ModelPath.Should().Be(targetPath);
    }

    [Theory]
    [InlineData("ClaimInput.UpperLimitManagementResult")]
    [InlineData("ClaimInput.UpperLimitManagedAmountYen")]
    public void Provider_combines_upper_limit_management_cross_field_condition_via_any(string targetPath)
    {
        // Phase 3-2 Task 5: field-mapping-r7-10.json（自己参照レグ、provider:J121:01:016/017）と
        // report-field-mapping-r8-06.json（クロスフィールドレグ、
        // modelPresent(Certificate.UpperLimitManagementProviderNumber)）が同一TargetPathへ合流し、
        // 条件文字列が異なるため CreateRequirement が Any(...) へラップする。この合流がspec §10の
        // 「UpperLimitManagementProviderNumberが非nullならこの2フィールドを必須化」を実現している
        // （自己参照レグ単体では恒久的にfail-openなため、クロスフィールドレグが唯一の実効ゲート）。
        var requirements = ClaimInputRequirementProvider.LoadEmbedded().GetRequirements();

        var requirement = requirements.Single(r => r.TargetPath == targetPath);
        var any = requirement.Condition.Should().BeOfType<ClaimRequirementCondition.Any>().Subject;
        any.Conditions.OfType<ClaimRequirementCondition.ModelPresent>().Should().Contain(
            modelPresent => modelPresent.ModelPath == "Certificate.UpperLimitManagementProviderNumber");
    }

    [Theory]
    [InlineData(
        "report:benefit-claim-form:header:004", "Office.PostalCode")]
    [InlineData(
        "report:benefit-claim-form:header:005", "Office.Address")]
    [InlineData(
        "report:benefit-claim-form:header:006", "Office.PhoneNumber")]
    [InlineData(
        "report:benefit-claim-form:header:008", "Office.RepresentativeTitleAndName")]
    public void Provider_registers_office_report_fields(string fieldId, string targetPath)
    {
        // Phase 3-2 Task 6: report-field-mapping-r8-06.jsonのOffice 4フィールド
        // （header:004-006, 008）がClaimInputRequirementとして登録され、
        // OfficeViewへ向くことを検証する（Task 4/5と同型のregistration pin）。
        var requirements = ClaimInputRequirementProvider.LoadEmbedded().GetRequirements();

        var requirement = requirements.Should().ContainSingle(r => r.TargetPath == targetPath).Subject;
        requirement.FieldIds.Should().Contain(fieldId);
        requirement.Destination.Should().Be(ClaimInputDestination.Office);
    }

    [Theory]
    [InlineData("Office.PostalCode")]
    [InlineData("Office.Address")]
    [InlineData("Office.PhoneNumber")]
    [InlineData("Office.RepresentativeTitleAndName")]
    public void Provider_keeps_office_report_fields_always_required(string targetPath)
    {
        // Phase 3-2 Task 6: Office 4フィールドは常時必須（spec §10）。MunicipalityNumberと同様、
        // 自己参照条件ではなくAlways条件のため、ClaimPreparationContextBuilderが実値
        // （office.PostalCode等）をIsPresentで判定でき、未入力を検知できる
        // （silently inertにならないことはClaimPreparationReadinessTests /
        // ClaimPreparationContextBuilderTestsのend-to-endで検証済み）。
        var requirements = ClaimInputRequirementProvider.LoadEmbedded().GetRequirements();

        var requirement = requirements.Single(r => r.TargetPath == targetPath);
        requirement.Condition.Should().BeOfType<ClaimRequirementCondition.Always>();
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
