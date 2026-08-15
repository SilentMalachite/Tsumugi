using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Application.UseCases.Backup;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.Application.Tests.Backup;

public sealed class RestoreDatabaseUseCaseTests
{
    private readonly List<string> _trace = [];

    private sealed class FakeLocation : IDatabaseFileLocation
    {
        public string DatabasePath => "/data/tsumugi.db";
        public string BackupDirectory => "/data/backups";
    }

    private sealed class TracingBackupService(List<string> trace) : IBackupService
    {
        public List<string> Destinations { get; } = [];
        public Task BackupToAsync(string destinationPath, CancellationToken ct)
        {
            Destinations.Add(destinationPath);
            trace.Add("backup");
            return Task.CompletedTask;
        }
    }

    private sealed class TracingRestoreService(List<string> trace) : IDatabaseRestoreService
    {
        public string? Source { get; private set; }
        public Task RestoreFromAsync(string backupFilePath, CancellationToken ct)
        {
            Source = backupFilePath;
            trace.Add("restore");
            return Task.CompletedTask;
        }
    }

    private sealed class TracingAuditTrail(List<string> trace) : IAuditTrail
    {
        public List<(AuditAction Action, string? Summary)> Records { get; } = [];
        public Task RecordAsync(string actor, AuditAction action, string targetType, Guid targetId,
            DateTimeOffset occurredAt, string? summary, CancellationToken ct)
        {
            Records.Add((action, summary));
            trace.Add("audit");
            return Task.CompletedTask;
        }
    }

    private sealed class TracingUnitOfWork(List<string> trace) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct)
        {
            trace.Add("save");
            return Task.FromResult(0);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 16, 17, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Runs_audit_save_snapshot_then_replace_in_that_order()
    {
        var backup = new TracingBackupService(_trace);
        var restore = new TracingRestoreService(_trace);
        var uc = new RestoreDatabaseUseCase(
            new FakeLocation(), backup, restore,
            new TracingAuditTrail(_trace), new TracingUnitOfWork(_trace),
            new FixedTimeProvider(Now));

        await uc.ExecuteAsync("tsumugi-backup-20260810-100000.db", "tester", CancellationToken.None);

        // 監査を先に保存してから退避を取ることで、退避スナップショットに復元の記録が載る。
        _trace.Should().Equal(["audit", "save", "backup", "restore"]);

        // 引数はバックアップディレクトリ直下のファイル名。ユースケースが結合する。
        restore.Source.Should().Be(Path.Combine("/data/backups", "tsumugi-backup-20260810-100000.db"));
    }

    [Fact]
    public async Task Snapshots_the_current_database_with_the_pre_restore_prefix()
    {
        var backup = new TracingBackupService(_trace);
        var uc = new RestoreDatabaseUseCase(
            new FakeLocation(), backup, new TracingRestoreService(_trace),
            new TracingAuditTrail(_trace), new TracingUnitOfWork(_trace),
            new FixedTimeProvider(Now));

        await uc.ExecuteAsync("tsumugi-backup-20260810-100000.db", "tester", CancellationToken.None);

        backup.Destinations.Should().ContainSingle()
            .Which.Should().EndWith("pre-restore-20260816-173000.db");
    }

    [Fact]
    public async Task Records_the_restore_with_file_names_but_no_full_path()
    {
        var audit = new TracingAuditTrail(_trace);
        var uc = new RestoreDatabaseUseCase(
            new FakeLocation(), new TracingBackupService(_trace), new TracingRestoreService(_trace),
            audit, new TracingUnitOfWork(_trace), new FixedTimeProvider(Now));

        await uc.ExecuteAsync("tsumugi-backup-20260810-100000.db", "tester", CancellationToken.None);

        var record = audit.Records.Should().ContainSingle().Subject;
        record.Action.Should().Be(AuditAction.Restore);
        record.Summary.Should().Contain("tsumugi-backup-20260810-100000.db");
        record.Summary.Should().Contain("pre-restore-20260816-173000.db");
        record.Summary.Should().NotContain("/data");
    }
}
