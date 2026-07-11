using FluentAssertions;
using Tsumugi.Domain.Logic.Claim.Models;

namespace Tsumugi.Domain.Tests.Entities;

public sealed class ClaimEvidenceTests
{
    [Fact]
    public void EnteredYen_distinguishes_not_entered_from_explicit_zero()
    {
        var missing = new EnteredYen(false, null);
        var zero = new EnteredYen(true, 0);

        missing.IsEntered.Should().BeFalse();
        missing.ValueYen.Should().BeNull();
        zero.IsEntered.Should().BeTrue();
        zero.ValueYen.Should().Be(0);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, null)]
    [InlineData(true, -1)]
    public void EnteredYen_rejects_inconsistent_state(bool isEntered, int? valueYen)
    {
        var act = () => new EnteredYen(isEntered, valueYen);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Average_wage_band_option_keeps_kind_and_official_code_separate()
    {
        var numeric = new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 8);
        var transition = new AverageWageBandOption(
            AverageWageBandOptionKind.FiledTransition,
            8);

        numeric.Kind.Should().Be(AverageWageBandOptionKind.Numeric);
        numeric.OfficialOptionCode.Should().Be(8);
        transition.Kind.Should().Be(AverageWageBandOptionKind.FiledTransition);
        transition.OfficialOptionCode.Should().Be(8);
        numeric.Should().NotBe(transition);
    }

    [Theory]
    [InlineData(AverageWageBandOptionKind.Unknown, 1)]
    [InlineData((AverageWageBandOptionKind)999, 1)]
    [InlineData(AverageWageBandOptionKind.Numeric, 0)]
    [InlineData(AverageWageBandOptionKind.Numeric, -1)]
    public void Average_wage_band_option_rejects_invalid_structure(
        AverageWageBandOptionKind kind,
        int officialOptionCode)
    {
        var act = () => new AverageWageBandOption(kind, officialOptionCode);

        act.Should().Throw<ArgumentException>();
    }
}
