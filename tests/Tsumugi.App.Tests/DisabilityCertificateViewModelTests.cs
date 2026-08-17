using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.UseCases.Certificate;
using Tsumugi.Application.UseCases.Recipient;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class DisabilityCertificateViewModelTests
{
    [Fact]
    public void New_view_model_exposes_renewal_and_consistency_collections()
    {
        var sut = new DisabilityCertificateViewModel(
            null!, null!, null!,
            new QueryDisabilityCertificateRenewalsUseCase(null!),
            new QueryDisabilityConsistencyUseCase(null!, null!));

        sut.ThresholdDays.Should().Be(30);
        sut.AsOfDate.Should().Be(DateOnly.FromDateTime(DateTime.Today));
        sut.RenewalDueItems.Should().BeEmpty();
        sut.ConsistencyWarnings.Should().BeEmpty();
    }
}
