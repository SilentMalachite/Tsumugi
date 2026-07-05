using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Application.UseCases.Wage;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests.Wage;

public sealed class SetRecipientHourlyRateUseCaseTests
{
    private static readonly Guid Office = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid Recipient = Guid.Parse("00000000-0000-0000-0000-000000000020");
    private static readonly DateRange Period = new(new DateOnly(2026, 4, 1), new DateOnly(2026, 9, 30));

    [Fact]
    public async Task Execute_persists_new_rate_and_audit_entry()
    {
        var repo = new FakeHourlyRateRepo();
        var uow = new FakeHourlyRateUow();
        var audit = new FakeHourlyRateAudit();
        var uc = new SetRecipientHourlyRateUseCase(repo, uow, audit, TimeProvider.System);

        var dto = await uc.ExecuteAsync(Office, Recipient, Period, 800, "alice", default);

        dto.HourlyYen.Should().Be(800);
        dto.OfficeId.Should().Be(Office);
        dto.RecipientId.Should().Be(Recipient);
        dto.Kind.Should().Be(RecordKind.New);
        repo.Added.Should().HaveCount(1);
        audit.Entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task Execute_maps_period_correctly()
    {
        var repo = new FakeHourlyRateRepo();
        var uow = new FakeHourlyRateUow();
        var audit = new FakeHourlyRateAudit();
        var uc = new SetRecipientHourlyRateUseCase(repo, uow, audit, TimeProvider.System);

        var dto = await uc.ExecuteAsync(Office, Recipient, Period, 1200, "alice", default);

        dto.Period.Start.Should().Be(Period.Start);
        dto.Period.End.Should().Be(Period.End);
        dto.OriginId.Should().BeNull();
    }

    [Fact]
    public async Task Execute_rejects_empty_actor()
    {
        var uc = new SetRecipientHourlyRateUseCase(
            new FakeHourlyRateRepo(), new FakeHourlyRateUow(),
            new FakeHourlyRateAudit(), TimeProvider.System);
        var act = async () => await uc.ExecuteAsync(Office, Recipient, Period, 800, "", default);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Execute_rejects_whitespace_actor()
    {
        var uc = new SetRecipientHourlyRateUseCase(
            new FakeHourlyRateRepo(), new FakeHourlyRateUow(),
            new FakeHourlyRateAudit(), TimeProvider.System);
        var act = async () => await uc.ExecuteAsync(Office, Recipient, Period, 800, "  ", default);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}

file sealed class FakeHourlyRateRepo : IRecipientHourlyRateRepository
{
    public List<RecipientHourlyRate> Added { get; } = new();
    public Task AddAsync(RecipientHourlyRate rate, CancellationToken ct)
    { Added.Add(rate); return Task.CompletedTask; }
    public Task<IReadOnlyList<RecipientHourlyRate>> ListByOfficeRecipientAsync(
        Guid officeId, Guid recipientId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RecipientHourlyRate>>(Added
            .Where(r => r.OfficeId == officeId && r.RecipientId == recipientId).ToArray());
}

file sealed class FakeHourlyRateUow : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(0);
}

file sealed class FakeHourlyRateAudit : IAuditTrail
{
    public List<(string Actor, AuditAction Action, string TargetType, Guid TargetId, string? Summary)> Entries { get; } = new();
    public Task RecordAsync(
        string actor, AuditAction action, string targetType, Guid targetId,
        DateTimeOffset occurredAt, string? summary, CancellationToken ct)
    { Entries.Add((actor, action, targetType, targetId, summary)); return Task.CompletedTask; }
}
