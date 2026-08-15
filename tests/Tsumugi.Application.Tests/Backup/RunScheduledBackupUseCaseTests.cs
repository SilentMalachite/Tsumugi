using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Application.UseCases.Backup;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.Application.Tests.Backup;

public sealed class RunScheduledBackupUseCaseTests
{
    private sealed class FakeLocation : IDatabaseFileLocation
    {
        public string DatabasePath => "/data/tsumugi.db";
        public string BackupDirectory => "/data/backups";
    }

    private sealed class FakeBackupService : IBackupService
    {
        public List<string> Destinations { get; } = [];
        public Task BackupToAsync(string destinationPath, CancellationToken ct)
        {
            Destinations.Add(destinationPath);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBackupDirectory(params string[] existing) : IBackupDirectory
    {
        public List<string> Deleted { get; } = [];
        public IReadOnlyList<string> ListFileNames() => existing;
        public void Delete(string fileName) => Deleted.Add(fileName);
    }

    private sealed class FakeAuditTrail : IAuditTrail
    {
        public List<(AuditAction Action, string TargetType, string? Summary)> Records { get; } = [];
        public Task RecordAsync(string actor, AuditAction action, string targetType, Guid targetId,
            DateTimeOffset occurredAt, string? summary, CancellationToken ct)
        {
            Records.Add((action, targetType, summary));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct) { SaveCount++; return Task.FromResult(0); }
    }

    private static readonly DateTimeOffset Now =
        new(2026, 8, 16, 17, 30, 0, TimeSpan.Zero);

    private static RunScheduledBackupUseCase Build(
        FakeBackupService backup, FakeBackupDirectory dir, FakeAuditTrail audit, FakeUnitOfWork uow)
        => new(new FakeLocation(), backup, dir, audit, uow,
               new FixedTimeProvider(Now));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task Writes_a_backup_named_from_the_current_time_into_the_backup_directory()
    {
        var backup = new FakeBackupService();
        var uc = Build(backup, new FakeBackupDirectory(), new FakeAuditTrail(), new FakeUnitOfWork());

        await uc.ExecuteAsync(CancellationToken.None);

        backup.Destinations.Should().ContainSingle()
            .Which.Should().EndWith("tsumugi-backup-20260816-173000.db");
    }

    [Fact]
    public async Task Deletes_the_generations_the_policy_selects()
    {
        var dir = new FakeBackupDirectory(
            "tsumugi-backup-20260816-090000.db",
            "tsumugi-backup-20260816-120000.db",
            "tsumugi-backup-20200101-000000.db",
            "pre-restore-20200101-000000.db");
        var uc = Build(new FakeBackupService(), dir, new FakeAuditTrail(), new FakeUnitOfWork());

        await uc.ExecuteAsync(CancellationToken.None);

        // 当日の古い2件 + 期限切れ1件。pre-restore は触らない。
        dir.Deleted.Should().BeEquivalentTo(
        [
            "tsumugi-backup-20260816-090000.db",
            "tsumugi-backup-20260816-120000.db",
            "tsumugi-backup-20200101-000000.db",
        ]);
    }

    [Fact]
    public async Task Records_an_audit_entry_without_a_full_path()
    {
        var audit = new FakeAuditTrail();
        var uow = new FakeUnitOfWork();
        var uc = Build(new FakeBackupService(), new FakeBackupDirectory(), audit, uow);

        await uc.ExecuteAsync(CancellationToken.None);

        var record = audit.Records.Should().ContainSingle().Subject;
        record.Action.Should().Be(AuditAction.Backup);
        record.TargetType.Should().Be("Database");
        record.Summary.Should().Contain("tsumugi-backup-20260816-173000.db");
        record.Summary.Should().NotContain("/data");   // フルパスを書かない（ハード制約4）
        uow.SaveCount.Should().Be(1);
    }
}
