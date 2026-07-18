using FluentAssertions;
using Tsumugi.Infrastructure.ClaimMasters;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Tests.ClaimMasters;

public sealed class JsonClaimMasterProviderCalculationMastersTests
{
    [Fact]
    public void ResolveCalculationMasters_filters_rows_by_effective_month()
    {
        var provider = JsonClaimMasterProvider.LoadEmbedded();
        var masters = provider.ResolveCalculationMasters(new ServiceMonth(2025, 4));
        masters.BasicRewards.Should().OnlyContain(row =>
            row.EffectiveFrom <= new ServiceMonth(2025, 4)
            && (row.EffectiveTo == null || new ServiceMonth(2025, 4) <= row.EffectiveTo));
    }

    [Fact]
    public void ResolveCalculationMasters_throws_for_month_before_any_release()
    {
        var provider = JsonClaimMasterProvider.LoadEmbedded();
        FluentActions.Invoking(() => provider.ResolveCalculationMasters(new ServiceMonth(2000, 1)))
            .Should().Throw<Tsumugi.Application.Abstractions.ClaimMasterPolicyUnavailableException>();
    }
}
