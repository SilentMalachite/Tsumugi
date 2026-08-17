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

    [Fact]
    public async Task Renewal_query_projects_mental_certificate_due_within_threshold()
    {
        var recipientId = Guid.NewGuid();
        var due = DisabilityCertificate.Create(
            Guid.NewGuid(), recipientId, DisabilityCertificateType.Mental, "2級",
            new DateOnly(2024, 4, 1), "東京都", "test", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
            nextRenewalDate: new DateOnly(2026, 4, 10));
        var repo = new FakeDisabilityCertificateRepository();
        repo.Added.Add(due);
        var sut = new QueryDisabilityCertificateRenewalsUseCase(repo);

        var result = await sut.ExecuteAsync(new DateOnly(2026, 4, 1), 30, default);

        result.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            CertificateId = due.Id,
            RecipientId = recipientId,
            Type = DisabilityCertificateType.Mental,
            Grade = "2級",
            NextRenewalDate = new DateOnly(2026, 4, 10),
            RemainingDays = 9,
        });
    }

    [Fact]
    public async Task Renewal_query_ignores_due_previous_version_when_current_version_is_not_due()
    {
        var recipientId = Guid.NewGuid();
        var previousDue = DisabilityCertificate.Create(
            Guid.NewGuid(), recipientId, DisabilityCertificateType.Mental, "2級",
            new DateOnly(2024, 4, 1), "東京都", "test", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
            nextRenewalDate: new DateOnly(2026, 4, 10));
        var currentNotDue = DisabilityCertificate.Create(
            Guid.NewGuid(), recipientId, DisabilityCertificateType.Mental, "1級",
            new DateOnly(2025, 4, 1), "東京都", "test", DateTimeOffset.UnixEpoch.AddDays(1), Guid.NewGuid(),
            nextRenewalDate: new DateOnly(2027, 4, 1));
        var repo = new FakeDisabilityCertificateRepository();
        repo.Added.AddRange([previousDue, currentNotDue]);
        var sut = new QueryDisabilityCertificateRenewalsUseCase(repo);

        var result = await sut.ExecuteAsync(new DateOnly(2026, 4, 1), 30, default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Renewal_query_uses_newer_created_at_when_issued_dates_are_equal()
    {
        var recipientId = Guid.NewGuid();
        var previousDue = DisabilityCertificate.Create(
            Guid.NewGuid(), recipientId, DisabilityCertificateType.Mental, "2級",
            new DateOnly(2024, 4, 1), "東京都", "test", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
            nextRenewalDate: new DateOnly(2026, 4, 10));
        var currentNotDue = DisabilityCertificate.Create(
            Guid.NewGuid(), recipientId, DisabilityCertificateType.Mental, "1級",
            new DateOnly(2024, 4, 1), "東京都", "test", DateTimeOffset.UnixEpoch.AddTicks(1), Guid.NewGuid(),
            nextRenewalDate: new DateOnly(2027, 4, 1));
        var repo = new FakeDisabilityCertificateRepository();
        repo.Added.AddRange([previousDue, currentNotDue]);
        var sut = new QueryDisabilityCertificateRenewalsUseCase(repo);

        var result = await sut.ExecuteAsync(new DateOnly(2026, 4, 1), 30, default);

        result.Should().BeEmpty();
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

    public Task<IReadOnlyList<DisabilityCertificate>> ListAllAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DisabilityCertificate>>(Added.ToArray());
}
