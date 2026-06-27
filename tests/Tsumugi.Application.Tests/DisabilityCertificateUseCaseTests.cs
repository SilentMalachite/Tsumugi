using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class DisabilityCertificateUseCaseTests
{
    [Fact]
    public async Task Register_adds_new_certificate_and_list_returns_it_newest_first()
    {
        var repo = new FakeDisabilityCertificateRepository();
        var uow = new FakeUnitOfWork();
        var sut = new RegisterDisabilityCertificateUseCase(
            repo, uow, new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        var rid = Guid.NewGuid();
        await sut.ExecuteAsync(rid, DisabilityCertificateType.Physical, "2級",
            new DateOnly(2020, 4, 1), "東京都", "u", default, subtype: "1種");
        await sut.ExecuteAsync(rid, DisabilityCertificateType.Physical, "1級",
            new DateOnly(2024, 4, 1), "東京都", "u", default, subtype: "1種",
            notes: "等級改定");

        var lister = new ListDisabilityCertificatesUseCase(repo);
        var list = await lister.ExecuteAsync(rid, default);

        list.Should().HaveCount(2);
        list[0].IssuedDate.Should().Be(new DateOnly(2024, 4, 1), "新しい交付日が先頭");
        list[0].Grade.Should().Be("1級");
        list[1].Grade.Should().Be("2級");
    }

    [Fact]
    public async Task Register_rejects_empty_recipient_id()
    {
        var sut = new RegisterDisabilityCertificateUseCase(
            new FakeDisabilityCertificateRepository(),
            new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        var act = () => sut.ExecuteAsync(Guid.Empty, DisabilityCertificateType.Mental, "1級",
            new DateOnly(2024, 4, 1), "東京都", "u", default);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}

internal sealed class FakeDisabilityCertificateRepository : IDisabilityCertificateRepository
{
    public List<DisabilityCertificate> Added { get; } = new();
    public Task AddAsync(DisabilityCertificate certificate, CancellationToken ct)
    { Added.Add(certificate); return Task.CompletedTask; }
    public Task<IReadOnlyList<DisabilityCertificate>> ListByRecipientAsync(
        Guid recipientId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DisabilityCertificate>>(
            Added.Where(c => c.RecipientId == recipientId)
                 .OrderByDescending(c => c.IssuedDate)
                 .ToArray());
}
