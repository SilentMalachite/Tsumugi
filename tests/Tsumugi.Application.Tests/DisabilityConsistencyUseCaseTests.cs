using FluentAssertions;
using Tsumugi.Application.UseCases.Certificate;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Tests;

public sealed class DisabilityConsistencyUseCaseTests
{
    [Fact]
    public async Task Consistency_query_uses_none_when_effective_certificate_is_missing()
    {
        var recipientId = Guid.NewGuid();
        var handbookRepo = new FakeDisabilityCertificateRepository();
        handbookRepo.Added.Add(DisabilityCertificate.Create(
            Guid.NewGuid(), recipientId, DisabilityCertificateType.Physical, "2級",
            new DateOnly(2024, 4, 1), "東京都", "test", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
        var sut = new QueryDisabilityConsistencyUseCase(
            new FakeCertificateRepository(), handbookRepo);

        var result = await sut.ExecuteAsync(recipientId, new DateOnly(2026, 4, 1), default);

        result.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            RecipientId = recipientId,
            Type = DisabilityCertificateType.Physical,
            Direction = DisabilityConsistencyDirection.HandbookOnly,
        });
    }

    [Fact]
    public async Task Consistency_query_selects_current_handbook_per_type_by_issued_then_created_at()
    {
        var recipientId = Guid.NewGuid();
        var handbookRepo = new FakeDisabilityCertificateRepository();
        handbookRepo.Added.Add(DisabilityCertificate.Create(
            Guid.NewGuid(), recipientId, DisabilityCertificateType.Physical, "3級",
            new DateOnly(2024, 4, 1), "東京都", "test", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
        handbookRepo.Added.Add(DisabilityCertificate.Create(
            Guid.NewGuid(), recipientId, DisabilityCertificateType.Physical, "2級",
            new DateOnly(2024, 4, 1), "東京都", "test", DateTimeOffset.UnixEpoch.AddHours(1), Guid.NewGuid()));
        handbookRepo.Added.Add(DisabilityCertificate.Create(
            Guid.NewGuid(), recipientId, DisabilityCertificateType.Mental, "2級",
            new DateOnly(2025, 4, 1), "東京都", "test", DateTimeOffset.UnixEpoch, Guid.NewGuid()));

        var certificateRepo = new FakeCertificateRepository();
        certificateRepo.Added.Add(Certificate.Create(
            Guid.NewGuid(),
            recipientId,
            "1234567890",
            new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
            20,
            0,
            "杉並区",
            "test",
            DateTimeOffset.UnixEpoch,
            Guid.NewGuid(),
            disabilities: new DisabilityCategories(
                Physical: true, Intellectual: false, Mental: false, Intractable: true)));

        var sut = new QueryDisabilityConsistencyUseCase(certificateRepo, handbookRepo);

        var result = await sut.ExecuteAsync(recipientId, new DateOnly(2026, 4, 1), default);

        result.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            RecipientId = recipientId,
            Type = DisabilityCertificateType.Mental,
            Direction = DisabilityConsistencyDirection.HandbookOnly,
        });
    }
}
