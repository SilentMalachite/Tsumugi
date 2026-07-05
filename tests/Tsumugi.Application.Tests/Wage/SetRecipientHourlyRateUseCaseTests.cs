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
        audit.Entries[0].Action.Should().Be(AuditAction.Register);
        audit.Entries[0].TargetType.Should().Be(nameof(RecipientHourlyRate));
        audit.Entries[0].TargetId.Should().Be(dto.Id);
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
    public async Task Execute_corrects_existing_period_instead_of_duplicating_new()
    {
        var repo = new FakeHourlyRateRepo();
        var audit = new FakeHourlyRateAudit();
        var uc = new SetRecipientHourlyRateUseCase(repo, new FakeHourlyRateUow(), audit, TimeProvider.System);

        var first = await uc.ExecuteAsync(Office, Recipient, Period, 800, "alice", default);
        var second = await uc.ExecuteAsync(Office, Recipient, Period, 850, "alice", default);

        second.Kind.Should().Be(RecordKind.Correct, "同一開始日の再保存は New の重複ではなく訂正になる");
        second.OriginId.Should().Be(first.Id);
        repo.Added.Count(r => r.Kind == RecordKind.New).Should().Be(1,
            "partial unique index (Kind=New, PeriodStart) と整合する");
        audit.Entries[1].Action.Should().Be(AuditAction.Update);
    }

    [Fact]
    public async Task Execute_correction_chains_hop_by_hop_on_third_save()
    {
        var repo = new FakeHourlyRateRepo();
        var uc = new SetRecipientHourlyRateUseCase(
            repo, new FakeHourlyRateUow(), new FakeHourlyRateAudit(), TimeProvider.System);

        await uc.ExecuteAsync(Office, Recipient, Period, 800, "alice", default);
        var second = await uc.ExecuteAsync(Office, Recipient, Period, 850, "alice", default);
        var third = await uc.ExecuteAsync(Office, Recipient, Period, 900, "alice", default);

        third.Kind.Should().Be(RecordKind.Correct);
        third.OriginId.Should().Be(second.Id, "訂正の訂正は直前レコードを指す（hop-by-hop 規約）");
    }

    [Fact]
    public async Task Execute_creates_new_record_for_different_period_start()
    {
        var repo = new FakeHourlyRateRepo();
        var uc = new SetRecipientHourlyRateUseCase(
            repo, new FakeHourlyRateUow(), new FakeHourlyRateAudit(), TimeProvider.System);

        await uc.ExecuteAsync(Office, Recipient, Period, 800, "alice", default);
        var laterPeriod = new DateRange(new DateOnly(2026, 10, 1), null);
        var second = await uc.ExecuteAsync(Office, Recipient, laterPeriod, 850, "alice", default);

        second.Kind.Should().Be(RecordKind.New, "開始日が異なる期間は独立した New になる");
        second.OriginId.Should().BeNull();
    }

    [Fact]
    public async Task Execute_rejects_reregistering_cancelled_period_start()
    {
        var repo = new FakeHourlyRateRepo();
        var uc = new SetRecipientHourlyRateUseCase(
            repo, new FakeHourlyRateUow(), new FakeHourlyRateAudit(), TimeProvider.System);
        var first = await uc.ExecuteAsync(Office, Recipient, Period, 800, "alice", default);
        repo.Added.Add(RecipientHourlyRate.Cancel(
            Guid.NewGuid(), Office, Recipient, Period, first.Id, "alice", DateTimeOffset.UtcNow));

        var act = async () => await uc.ExecuteAsync(Office, Recipient, Period, 850, "alice", default);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*取消済み*");
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
    public Task<IReadOnlyList<RecipientHourlyRate>> ListByOfficeAsync(
        Guid officeId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RecipientHourlyRate>>(Added
            .Where(r => r.OfficeId == officeId).ToArray());
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
