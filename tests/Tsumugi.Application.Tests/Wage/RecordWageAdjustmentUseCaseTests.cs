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
    public async Task Execute_corrects_existing_record_instead_of_duplicating_new()
    {
        var repo = new FakeAdjustmentRepo();
        var uow = new FakeUow();
        var audit = new FakeAudit();
        var uc = new RecordWageAdjustmentUseCase(repo, uow, audit, TimeProvider.System);

        var first = await uc.ExecuteAsync(Office, Recipient, Ym,
            WageAdjustmentType.SpecialAllowance, 1000, null, "tester", default);
        var second = await uc.ExecuteAsync(Office, Recipient, Ym,
            WageAdjustmentType.SpecialAllowance, 1500, null, "tester", default);

        second.Kind.Should().Be(RecordKind.Correct, "同一キーの再保存は New の重複ではなく訂正になる");
        second.OriginId.Should().Be(first.Id);
        repo.Added.Should().HaveCount(2);
        repo.Added.Count(a => a.Kind == RecordKind.New).Should().Be(1,
            "partial unique index (Kind=New) と整合する");
        audit.Entries[1].Action.Should().Be(AuditAction.Update);
    }

    [Fact]
    public async Task Execute_correction_chains_hop_by_hop_on_third_save()
    {
        var repo = new FakeAdjustmentRepo();
        var uc = new RecordWageAdjustmentUseCase(repo, new FakeUow(), new FakeAudit(), TimeProvider.System);

        await uc.ExecuteAsync(Office, Recipient, Ym,
            WageAdjustmentType.SpecialAllowance, 1000, null, "tester", default);
        var second = await uc.ExecuteAsync(Office, Recipient, Ym,
            WageAdjustmentType.SpecialAllowance, 1500, null, "tester", default);
        var third = await uc.ExecuteAsync(Office, Recipient, Ym,
            WageAdjustmentType.SpecialAllowance, 2000, null, "tester", default);

        third.Kind.Should().Be(RecordKind.Correct);
        third.OriginId.Should().Be(second.Id, "訂正の訂正は直前レコードを指す（hop-by-hop 規約）");
    }

    [Fact]
    public async Task ExecuteMany_saves_all_entries_with_single_commit()
    {
        var repo = new FakeAdjustmentRepo();
        var uow = new FakeUow();
        var audit = new FakeAudit();
        var uc = new RecordWageAdjustmentUseCase(repo, uow, audit, TimeProvider.System);
        var recipientB = Guid.Parse("00000000-0000-0000-0000-000000000021");

        var dtos = await uc.ExecuteManyAsync(Office,
            new[] { (Recipient, 1000), (recipientB, 2000) },
            Ym, WageAdjustmentType.SpecialAllowance, null, "tester", default);

        dtos.Should().HaveCount(2);
        repo.Added.Should().HaveCount(2);
        audit.Entries.Should().HaveCount(2);
        uow.SaveCount.Should().Be(1, "複数行の保存は 1 トランザクションで行う");
    }

    [Fact]
    public async Task ExecuteMany_chains_hop_by_hop_when_same_recipient_appears_twice_in_batch()
    {
        // 同一利用者が同一バッチ内で2回指定された場合、2件目は1件目の New と衝突する
        // 重複 New ではなく、1件目を起点にした Correction にならなければならない
        var repo = new FakeAdjustmentRepo();
        var uow = new FakeUow();
        var audit = new FakeAudit();
        var uc = new RecordWageAdjustmentUseCase(repo, uow, audit, TimeProvider.System);

        var dtos = await uc.ExecuteManyAsync(Office,
            new[] { (Recipient, 1000), (Recipient, 1500) },
            Ym, WageAdjustmentType.SpecialAllowance, null, "tester", default);

        dtos.Should().HaveCount(2);
        dtos[0].Kind.Should().Be(RecordKind.New);
        dtos[1].Kind.Should().Be(RecordKind.Correct, "同一バッチ内の2回目は1回目への訂正になる");
        dtos[1].OriginId.Should().Be(dtos[0].Id);
        repo.Added.Count(a => a.Kind == RecordKind.New).Should().Be(1,
            "partial unique index (Kind=New) と整合する");
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteMany_returns_empty_without_commit_when_no_entries()
    {
        var uow = new FakeUow();
        var uc = new RecordWageAdjustmentUseCase(
            new FakeAdjustmentRepo(), uow, new FakeAudit(), TimeProvider.System);

        var dtos = await uc.ExecuteManyAsync(Office,
            Array.Empty<(Guid, int)>(), Ym,
            WageAdjustmentType.SpecialAllowance, null, "tester", default);

        dtos.Should().BeEmpty();
        uow.SaveCount.Should().Be(0);
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
    public int SaveCount { get; private set; }
    public Task<int> SaveChangesAsync(CancellationToken ct)
    { SaveCount++; return Task.FromResult(0); }
}

file sealed class FakeAudit : IAuditTrail
{
    public List<(string Actor, AuditAction Action, string TargetType, Guid TargetId, string? Summary)> Entries { get; } = new();
    public Task RecordAsync(
        string actor, AuditAction action, string targetType, Guid targetId,
        DateTimeOffset occurredAt, string? summary, CancellationToken ct)
    { Entries.Add((actor, action, targetType, targetId, summary)); return Task.CompletedTask; }
}
