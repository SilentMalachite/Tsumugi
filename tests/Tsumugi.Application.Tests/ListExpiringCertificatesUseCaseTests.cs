using FluentAssertions;
using Tsumugi.Application.UseCases.Certificate;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class ListExpiringCertificatesUseCaseTests
{
    [Fact]
    public async Task Returns_expiring_within_threshold()
    {
        var repo = new FakeCertificateRepository();
        repo.Added.Add(Certificate.Create(Guid.NewGuid(), Guid.NewGuid(), "near",
            new DateRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 1)),
            0, 0, "x", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
        repo.Added.Add(Certificate.Create(Guid.NewGuid(), Guid.NewGuid(), "far",
            new DateRange(new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1)),
            0, 0, "x", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));

        var sut = new ListExpiringCertificatesUseCase(repo);
        var dtos = await sut.ExecuteAsync(new DateOnly(2026, 6, 27), 30, default);

        dtos.Should().ContainSingle();
        dtos[0].CertificateNumber.Should().Be("near");
        dtos[0].RemainingDays.Should().Be(4);
    }
}
