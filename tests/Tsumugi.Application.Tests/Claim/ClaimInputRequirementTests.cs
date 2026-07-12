using FluentAssertions;
using Tsumugi.Application.Claim;
using Xunit;

namespace Tsumugi.Application.Tests.Claim;

public sealed class ClaimInputRequirementTests
{
    [Fact]
    public void Condition_tree_retains_typed_values_and_copies_collections()
    {
        var allowedValues = new List<string> { "TypeI", "TypeII" };
        var modelIn = new ClaimRequirementCondition.ModelIn(
            "DailyRecord.MedicalCoordinationType", allowedValues);
        var children = new List<ClaimRequirementCondition>
        {
            new ClaimRequirementCondition.RowPresent("service-performance.daily"),
            modelIn,
        };
        var all = new ClaimRequirementCondition.All(children);
        var any = new ClaimRequirementCondition.Any(
        [
            all,
            new ClaimRequirementCondition.ModelPresent("Certificate.Number"),
            new ClaimRequirementCondition.ModelNonZero("ClaimInput.Amount"),
            new ClaimRequirementCondition.ModelTrue("DailyRecord.Applied"),
            new ClaimRequirementCondition.Always(),
        ]);

        allowedValues.Add("TypeIII");
        children.Clear();

        modelIn.AllowedValues.Should().Equal("TypeI", "TypeII");
        all.Conditions.Should().HaveCount(2);
        any.Conditions.Should().HaveCount(5);
    }

    [Fact]
    public void Requirement_copies_field_ids_and_exposes_closed_destination()
    {
        var fieldIds = new List<string> { "field:001", "field:002" };
        var requirement = new ClaimInputRequirement(
            "ClaimInput.Amount",
            fieldIds,
            new ClaimRequirementCondition.Always(),
            ClaimInputDestination.ClaimInput);

        fieldIds.Clear();

        requirement.TargetPath.Should().Be("ClaimInput.Amount");
        requirement.FieldIds.Should().Equal("field:001", "field:002");
        requirement.Destination.Should().Be(ClaimInputDestination.ClaimInput);
        Enum.GetValues<ClaimInputDestination>().Should().Equal(
            ClaimInputDestination.Unknown,
            ClaimInputDestination.Certificate,
            ClaimInputDestination.ClaimInput,
            ClaimInputDestination.ClaimPreparation,
            ClaimInputDestination.DailyRecord,
            ClaimInputDestination.Office);
    }

    [Fact]
    public void Model_in_rejects_invalid_values()
    {
        var blankPath = () => new ClaimRequirementCondition.ModelIn(" ", ["TypeI"]);
        var nullValues = () => new ClaimRequirementCondition.ModelIn("Model.Value", null!);
        var emptyValues = () => new ClaimRequirementCondition.ModelIn("Model.Value", []);
        var blankValue = () => new ClaimRequirementCondition.ModelIn("Model.Value", [" "]);
        var duplicateValues = () => new ClaimRequirementCondition.ModelIn(
            "Model.Value", ["TypeI", "TypeI"]);

        blankPath.Should().Throw<ArgumentException>();
        nullValues.Should().Throw<ArgumentNullException>();
        emptyValues.Should().Throw<ArgumentException>();
        blankValue.Should().Throw<ArgumentException>();
        duplicateValues.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Composite_conditions_reject_invalid_children()
    {
        var nullAll = () => new ClaimRequirementCondition.All(null!);
        var emptyAll = () => new ClaimRequirementCondition.All([]);
        var nullChild = () => new ClaimRequirementCondition.Any(
            new ClaimRequirementCondition[] { null! });

        nullAll.Should().Throw<ArgumentNullException>();
        emptyAll.Should().Throw<ArgumentException>();
        nullChild.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Requirement_rejects_invalid_contract_values()
    {
        var condition = new ClaimRequirementCondition.Always();
        var blankTarget = () => new ClaimInputRequirement(
            " ", ["field:001"], condition, ClaimInputDestination.ClaimInput);
        var nullFields = () => new ClaimInputRequirement(
            "Model.Value", null!, condition, ClaimInputDestination.ClaimInput);
        var emptyFields = () => new ClaimInputRequirement(
            "Model.Value", [], condition, ClaimInputDestination.ClaimInput);
        var blankField = () => new ClaimInputRequirement(
            "Model.Value", [" "], condition, ClaimInputDestination.ClaimInput);
        var duplicateFields = () => new ClaimInputRequirement(
            "Model.Value", ["field:001", "field:001"], condition,
            ClaimInputDestination.ClaimInput);
        var nullCondition = () => new ClaimInputRequirement(
            "Model.Value", ["field:001"], null!, ClaimInputDestination.ClaimInput);
        var unknownDestination = () => new ClaimInputRequirement(
            "Model.Value", ["field:001"], condition, ClaimInputDestination.Unknown);

        blankTarget.Should().Throw<ArgumentException>();
        nullFields.Should().Throw<ArgumentNullException>();
        emptyFields.Should().Throw<ArgumentException>();
        blankField.Should().Throw<ArgumentException>();
        duplicateFields.Should().Throw<ArgumentException>();
        nullCondition.Should().Throw<ArgumentNullException>();
        unknownDestination.Should().Throw<ArgumentOutOfRangeException>();
    }
}
