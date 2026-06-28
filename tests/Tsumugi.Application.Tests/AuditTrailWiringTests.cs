using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class AuditTrailWiringTests
{
    private sealed class FakeOfficeRepoLocal : IOfficeRepository
    {
        public Office? Existing { get; init; }
        public Task AddAsync(Office o, CancellationToken ct) => Task.CompletedTask;
        public Task<Office?> FindByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Existing?.Id == id ? Existing : null);
        public Task<Office?> FindByNumberAsync(string n, CancellationToken ct) =>
            Task.FromResult<Office?>(null);
        public Task UpdateAsync(Office o, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<Office>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Office>>(Existing is null ? Array.Empty<Office>() : new[] { Existing });
    }

    private static readonly DateTimeOffset T0 = new(2026, 7, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task UpdateOffice_emits_audit_entry()
    {
        var token = Guid.NewGuid();
        var existing = Office.Create(Guid.NewGuid(), "1234567890", "旧名",
            ServiceCategory.TypeB, RegionGrade.None, "u", T0, token);
        var repo = new FakeOfficeRepoLocal { Existing = existing };
        var audit = new RecordingAuditTrail();
        var sut = new UpdateOfficeUseCase(repo, new FakeUnitOfWork(),
            new FixedTimeProvider(T0), audit);

        await sut.ExecuteAsync(existing.Id, token, "新名",
            ServiceCategory.TypeB, RegionGrade.Grade2, "alice", default);

        audit.Calls.Should().ContainSingle();
        var call = audit.Calls[0];
        call.Actor.Should().Be("alice");
        call.Action.Should().Be(AuditAction.Update);
        call.TargetType.Should().Be(nameof(Office));
        call.TargetId.Should().Be(existing.Id);
        call.Summary.Should().Contain("新名");
    }

    [Fact]
    public async Task UpdateOffice_rejects_blank_actor()
    {
        var token = Guid.NewGuid();
        var existing = Office.Create(Guid.NewGuid(), "1234567890", "n",
            ServiceCategory.TypeB, RegionGrade.None, "u", T0, token);
        var repo = new FakeOfficeRepoLocal { Existing = existing };
        var sut = new UpdateOfficeUseCase(repo, new FakeUnitOfWork(),
            new FixedTimeProvider(T0), new NoopAuditTrail());

        var act = async () => await sut.ExecuteAsync(existing.Id, token, "新名",
            ServiceCategory.TypeB, RegionGrade.None, "  ", default);
        await act.Should().ThrowAsync<ArgumentException>().Where(e => e.ParamName == "actor");
    }

    [Fact]
    public async Task UpdateRecipient_emits_audit_entry()
    {
        var token = Guid.NewGuid();
        var existing = Recipient.Create(Guid.NewGuid(), "旧名", "キュウ",
            new DateOnly(1990, 1, 1), "u", T0, token);
        var repo = new FakeRecipientRepository();
        repo.Added.Add(existing);
        var audit = new RecordingAuditTrail();
        var sut = new UpdateRecipientUseCase(repo, new FakeUnitOfWork(),
            new FixedTimeProvider(T0), audit);

        var input = new UpdateRecipientInput(existing.Id, token, "新名", "シン", new DateOnly(1990, 1, 1));
        await sut.ExecuteAsync(input, "alice", default);

        audit.Calls.Should().ContainSingle()
            .Which.Should().Match<AuditCall>(c =>
                c.Actor == "alice"
                && c.Action == AuditAction.Update
                && c.TargetType == nameof(Recipient)
                && c.TargetId == existing.Id);
    }

    [Fact]
    public async Task ArchiveRecipient_emits_audit_entry_with_archive_action()
    {
        var token = Guid.NewGuid();
        var existing = Recipient.Create(Guid.NewGuid(), "削除一郎", "サクジョ",
            new DateOnly(1990, 1, 1), "u", T0, token);
        var repo = new FakeRecipientRepository();
        repo.Added.Add(existing);
        var audit = new RecordingAuditTrail();
        var sut = new ArchiveRecipientUseCase(repo, new FakeUnitOfWork(),
            new FixedTimeProvider(T0), audit);

        await sut.ExecuteAsync(existing.Id, token, "alice", default);

        audit.Calls.Should().ContainSingle()
            .Which.Action.Should().Be(AuditAction.Archive);
    }

    [Fact]
    public async Task RestoreRecipient_emits_audit_entry_with_restore_action()
    {
        var token = Guid.NewGuid();
        var existing = Recipient.Create(Guid.NewGuid(), "復元次郎", "フクゲン",
            new DateOnly(1985, 1, 1), "u", T0, token)
            .Archive("u", T0.AddDays(1));
        var repo = new FakeRecipientRepository();
        repo.Added.Add(existing);
        var audit = new RecordingAuditTrail();
        var sut = new RestoreRecipientUseCase(repo, new FakeUnitOfWork(),
            new FixedTimeProvider(T0), audit);

        await sut.ExecuteAsync(existing.Id, token, "alice", default);

        audit.Calls.Should().ContainSingle()
            .Which.Action.Should().Be(AuditAction.Restore);
    }
}
