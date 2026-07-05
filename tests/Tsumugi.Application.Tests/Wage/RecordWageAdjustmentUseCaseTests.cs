using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Application.UseCases.Wage;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests.Wage;

public sealed class RecordWageAdjustmentUseCaseTests
{
    private static readonly Guid Office = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid Recipient = Guid.Parse("00000000-0000-0000-0000-000000000020");
    private static readonly YearMonth Ym = YearMonth.FromInt(202605);

    [Fact]
    public async Task Execute_persists_new_record_and_audit_entry()
    {
        var repo = new FakeAdjustmentRepo();
        var uow = new FakeUow();
        var audit = new FakeAudit();
        var uc = new RecordWageAdjustmentUseCase(repo, uow, audit, TimeProvider.System);

        var dto = await uc.ExecuteAsync(Office, Recipient, Ym,
            WageAdjustmentType.SpecialAllowance, 1000, "月末調整", "tester", default);

        dto.AmountYen.Should().Be(1000);
        dto.Type.Should().Be(WageAdjustmentType.SpecialAllowance);
        dto.YearMonth.Should().Be(Ym);
        repo.Added.Should().HaveCount(1);
        audit.Entries.Should().HaveCount(1);
        audit.Entries[0].Action.Should().Be(AuditAction.Register);
        audit.Entries[0].TargetType.Should().Be(nameof(WageAdjustment));
        audit.Entries[0].TargetId.Should().Be(dto.Id);
    }

    [Fact]
    public async Task Execute_maps_all_dto_fields_correctly()
    {
        var repo = new FakeAdjustmentRepo();
        var uow = new FakeUow();
        var audit = new FakeAudit();
        var uc = new RecordWageAdjustmentUseCase(repo, uow, audit, TimeProvider.System);

        var dto = await uc.ExecuteAsync(Office, Recipient, Ym,
            WageAdjustmentType.SpecialAllowance, 500, "テスト", "alice", default);

        dto.OfficeId.Should().Be(Office);
        dto.RecipientId.Should().Be(Recipient);
        dto.Kind.Should().Be(RecordKind.New);
        dto.OriginId.Should().BeNull();
        dto.Note.Should().Be("テスト");
    }

    [Fact]
    public async Task Execute_rejects_empty_actor()
    {
        var uc = new RecordWageAdjustmentUseCase(
            new FakeAdjustmentRepo(), new FakeUow(),
            new FakeAudit(), TimeProvider.System);
        var act = async () => await uc.ExecuteAsync(Office, Recipient, Ym,
            WageAdjustmentType.SpecialAllowance, 1000, null, "", default);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Execute_rejects_whitespace_actor()
    {
        var uc = new RecordWageAdjustmentUseCase(
            new FakeAdjustmentRepo(), new FakeUow(),
            new FakeAudit(), TimeProvider.System);
        var act = async () => await uc.ExecuteAsync(Office, Recipient, Ym,
            WageAdjustmentType.SpecialAllowance, 1000, null, "   ", default);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}

file sealed class FakeAdjustmentRepo : IWageAdjustmentRepository
{
    public List<WageAdjustment> Added { get; } = new();
    public Task AddAsync(WageAdjustment adjustment, CancellationToken ct)
    { Added.Add(adjustment); return Task.CompletedTask; }
    public Task<IReadOnlyList<WageAdjustment>> ListByOfficeMonthAsync(
        Guid officeId, YearMonth ym, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<WageAdjustment>>(Added
            .Where(a => a.OfficeId == officeId && a.YearMonth == ym).ToArray());
}

file sealed class FakeUow : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(0);
}

file sealed class FakeAudit : IAuditTrail
{
    public List<(string Actor, AuditAction Action, string TargetType, Guid TargetId, string? Summary)> Entries { get; } = new();
    public Task RecordAsync(
        string actor, AuditAction action, string targetType, Guid targetId,
        DateTimeOffset occurredAt, string? summary, CancellationToken ct)
    { Entries.Add((actor, action, targetType, targetId, summary)); return Task.CompletedTask; }
}
