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

    /// <summary>
    /// FakeBackupDirectory と同じ <see cref="List{T}"/> を共有し、BackupToAsync で書いたファイル名を
    /// そのリストへ追加する。本番の Infrastructure 実装では BackupToAsync が物理的に書き終えてから
    /// 戻るため、その後の ListFileNames()（実装は Directory.EnumerateFiles）には既に新しいファイルが
    /// 含まれる。この関係を fake 側で正しく代理するための実装で、ユースケース側に手当ては不要。
    /// </summary>
    private sealed class FakeBackupService(List<string> files) : IBackupService
    {
        public List<string> Destinations { get; } = [];
        public Task BackupToAsync(string destinationPath, CancellationToken ct)
        {
            Destinations.Add(destinationPath);
            files.Add(Path.GetFileName(destinationPath));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBackupDirectory : IBackupDirectory
    {
        /// <summary>FakeBackupService と共有する実体。BackupToAsync がここへ書き込んだファイル名を追加する。</summary>
        public List<string> Files { get; }

        public List<string> Deleted { get; } = [];

        public FakeBackupDirectory(params string[] existing) => Files = [.. existing];

        public IReadOnlyList<string> ListFileNames() => Files;
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

    /// <summary>
    /// FakeBackupService は渡された dir.Files を共有するため、ここで dir から組み立てる
    /// （BackupToAsync の書き込みが ListFileNames() に反映される、という本番と同じ関係を保つため）。
    /// </summary>
    private static (RunScheduledBackupUseCase UseCase, FakeBackupService Backup) Build(
        FakeBackupDirectory dir, FakeAuditTrail audit, FakeUnitOfWork uow)
    {
        var backup = new FakeBackupService(dir.Files);
        var uc = new RunScheduledBackupUseCase(new FakeLocation(), backup, dir, audit, uow,
            new FixedTimeProvider(Now));
        return (uc, backup);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task Writes_a_backup_named_from_the_current_time_into_the_backup_directory()
    {
        var (uc, backup) = Build(new FakeBackupDirectory(), new FakeAuditTrail(), new FakeUnitOfWork());

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
        var (uc, _) = Build(dir, new FakeAuditTrail(), new FakeUnitOfWork());

        await uc.ExecuteAsync(CancellationToken.None);

        // 実行直後に書かれた 17:30 のバックアップが当日の生き残りになるため（規則2: 同日は最新1つだけ残す）、
        // 当日の既存2件（09:00・12:00）はどちらも削除対象になる。加えて規則3（直近7日分だけ残す）により
        // 2020-01-01 の1件も削除対象。pre-restore は規則1（命名規則外・退避は対象外）で触らない。
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
        var (uc, _) = Build(new FakeBackupDirectory(), audit, uow);

        await uc.ExecuteAsync(CancellationToken.None);

        var record = audit.Records.Should().ContainSingle().Subject;
        record.Action.Should().Be(AuditAction.Backup);
        record.TargetType.Should().Be("Database");
        // 完全一致で検証する: ファイル名だけを含み、保存先ディレクトリ（/data/backups）を含まないことを
        // 部分一致ではなく文字列全体で担保する（ハード制約4: ログ・監査にフルパスを書かない）。
        record.Summary.Should().Be("自動バックアップ tsumugi-backup-20260816-173000.db（世代削除 0 件）");
        uow.SaveCount.Should().Be(1);
    }
}
