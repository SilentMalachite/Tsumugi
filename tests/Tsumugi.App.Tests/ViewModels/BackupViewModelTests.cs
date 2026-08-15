using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Tsumugi.App.Services;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Application.UseCases.Backup;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.App.Tests.ViewModels;

public sealed class BackupViewModelTests
{
    private sealed class FakeBackupService : IBackupService
    {
        public List<string> Destinations { get; } = [];
        public byte[] Payload { get; set; } = [1, 2, 3];
        public Exception? Throws { get; set; }
        public Task BackupToAsync(string destinationPath, CancellationToken ct)
        {
            if (Throws is not null) throw Throws;
            Destinations.Add(destinationPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            return File.WriteAllBytesAsync(destinationPath, Payload, ct);
        }
    }

    private sealed class FakeBackupDirectory : IBackupDirectory
    {
        public List<string> Files { get; } = [];
        public List<string> Deleted { get; } = [];
        public IReadOnlyList<string> ListFileNames() => Files.ToArray();
        public void Delete(string fileName) { Deleted.Add(fileName); Files.Remove(fileName); }
    }

    private sealed class FakeRestoreService : IDatabaseRestoreService
    {
        public string? Source { get; private set; }
        public Exception? Throws { get; set; }
        public Task RestoreFromAsync(string backupFilePath, CancellationToken ct)
        {
            if (Throws is not null) throw Throws;
            Source = backupFilePath;
            return Task.CompletedTask;
        }
    }

    private sealed class NullAuditTrail : IAuditTrail
    {
        public Task RecordAsync(string actor, AuditAction action, string targetType, Guid targetId,
            DateTimeOffset occurredAt, string? summary, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class NullUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(0);
    }

    private sealed class FakeFileSave : IFileSaveService
    {
        public byte[]? Saved { get; private set; }
        public string? SuggestedFileName { get; private set; }
        public bool Result { get; set; } = true;
        public Task<bool> SaveAsync(byte[] bytes, string suggestedFileName, string fileTypeName,
            string extension, CancellationToken ct = default)
        {
            Saved = bytes;
            SuggestedFileName = suggestedFileName;
            return Task.FromResult(Result);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeApplicationShutdown : IApplicationShutdown
    {
        public int CallCount { get; private set; }
        public void RequestShutdown() => CallCount++;
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 16, 17, 30, 0, TimeSpan.Zero);

    private sealed record Harness(
        BackupViewModel ViewModel,
        FakeBackupDirectory Directory,
        FakeBackupService BackupService,
        FakeRestoreService Restore,
        FakeFileSave FileSave,
        FakeApplicationShutdown Shutdown,
        string BackupDirectoryPath);

    private static Harness Build(string tempRoot)
    {
        var location = new TempLocation(tempRoot);
        var backup = new FakeBackupService();
        var dir = new FakeBackupDirectory();
        var restore = new FakeRestoreService();
        var fileSave = new FakeFileSave();
        var shutdown = new FakeApplicationShutdown();
        var clock = new FixedTimeProvider(Now);

        var run = new RunScheduledBackupUseCase(
            location, backup, dir, new NullAuditTrail(), new NullUnitOfWork(), clock);
        var list = new ListBackupGenerationsUseCase(dir);
        var restoreUc = new RestoreDatabaseUseCase(
            location, backup, restore, new NullAuditTrail(), new NullUnitOfWork(), clock);
        var export = new ExportBackupCopyUseCase(location, backup, clock);

        return new Harness(
            new BackupViewModel(run, list, restoreUc, export, fileSave, shutdown),
            dir, backup, restore, fileSave, shutdown, location.BackupDirectory);
    }

    private sealed class TempLocation(string root) : IDatabaseFileLocation
    {
        public string DatabasePath => Path.Combine(root, "tsumugi.db");
        public string BackupDirectory => Path.Combine(root, "backups");
    }

    private static string NewTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "tsumugi-vm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "backups"));
        return root;
    }

    [Fact]
    public async Task LoadAsync_lists_generations_newest_first()
    {
        var root = NewTempRoot();
        var h = Build(root);
        h.Directory.Files.AddRange(
        [
            "tsumugi-backup-20260810-100000.db",
            "tsumugi-backup-20260816-100000.db",
        ]);

        await h.ViewModel.LoadAsync();

        h.ViewModel.Generations.First().Should().Be("tsumugi-backup-20260816-100000.db");
    }

    [Fact]
    public async Task RestoreAsync_does_nothing_when_no_generation_is_selected()
    {
        var h = Build(NewTempRoot());
        h.ViewModel.SelectedGeneration = null;

        await h.ViewModel.RestoreAsync();

        h.Restore.Source.Should().BeNull();
        h.ViewModel.RestartRequired.Should().BeFalse();
        h.ViewModel.RestoreArmed.Should().BeFalse();
    }

    [Fact]
    public async Task RestoreAsync_first_call_arms_the_confirmation_without_executing()
    {
        var h = Build(NewTempRoot());
        h.ViewModel.SelectedGeneration = "tsumugi-backup-20260810-100000.db";

        await h.ViewModel.RestoreAsync();

        h.Restore.Source.Should().BeNull();
        h.ViewModel.RestartRequired.Should().BeFalse();
        h.ViewModel.RestoreArmed.Should().BeTrue();
        h.ViewModel.StatusMessage.Should().Contain("もう一度");
    }

    [Fact]
    public async Task RestoreAsync_second_call_executes_and_sets_restart_required()
    {
        var h = Build(NewTempRoot());
        h.ViewModel.SelectedGeneration = "tsumugi-backup-20260810-100000.db";

        await h.ViewModel.RestoreAsync();
        await h.ViewModel.RestoreAsync();

        h.Restore.Source.Should().NotBeNull();
        h.ViewModel.RestartRequired.Should().BeTrue();
        h.ViewModel.RestoreArmed.Should().BeFalse();
        h.ViewModel.StatusMessage.Should().Contain("再起動");
        h.ViewModel.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task RestoreAsync_first_call_does_not_request_shutdown()
    {
        var h = Build(NewTempRoot());
        h.ViewModel.SelectedGeneration = "tsumugi-backup-20260810-100000.db";

        await h.ViewModel.RestoreAsync();

        h.Shutdown.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task RestoreAsync_second_call_requests_shutdown_exactly_once()
    {
        var h = Build(NewTempRoot());
        h.ViewModel.SelectedGeneration = "tsumugi-backup-20260810-100000.db";

        await h.ViewModel.RestoreAsync();
        await h.ViewModel.RestoreAsync();

        h.Shutdown.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task RestoreAsync_does_not_request_shutdown_when_restore_throws()
    {
        var h = Build(NewTempRoot());
        h.Restore.Throws = new IOException("置換に失敗");
        h.ViewModel.SelectedGeneration = "tsumugi-backup-20260810-100000.db";

        await h.ViewModel.RestoreAsync();
        await h.ViewModel.RestoreAsync();

        h.Shutdown.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task RestoreAsync_disarms_when_the_selected_generation_changes()
    {
        var h = Build(NewTempRoot());
        h.ViewModel.SelectedGeneration = "tsumugi-backup-20260810-100000.db";
        await h.ViewModel.RestoreAsync();
        h.ViewModel.RestoreArmed.Should().BeTrue();

        h.ViewModel.SelectedGeneration = "tsumugi-backup-20260816-100000.db";

        h.ViewModel.RestoreArmed.Should().BeFalse();
    }

    [Fact]
    public async Task RestoreAsync_surfaces_a_failure_without_claiming_success()
    {
        var h = Build(NewTempRoot());
        h.Restore.Throws = new IOException("置換に失敗");
        h.ViewModel.SelectedGeneration = "tsumugi-backup-20260810-100000.db";

        await h.ViewModel.RestoreAsync();
        await h.ViewModel.RestoreAsync();

        h.ViewModel.RestartRequired.Should().BeFalse();
        h.ViewModel.RestoreArmed.Should().BeFalse();
        h.ViewModel.ErrorMessage.Should().Contain("復元に失敗");
    }

    [Fact]
    public async Task RestoreAsync_failure_message_does_not_leak_the_full_path_from_the_exception()
    {
        var h = Build(NewTempRoot());
        h.Restore.Throws = new IOException(
            "/Users/someone/Library/Application Support/Tsumugi/tsumugi.db は使用中です");
        h.ViewModel.SelectedGeneration = "tsumugi-backup-20260810-100000.db";

        await h.ViewModel.RestoreAsync();
        await h.ViewModel.RestoreAsync();

        h.ViewModel.ErrorMessage.Should().NotBeNullOrEmpty();
        h.ViewModel.ErrorMessage.Should().NotContain("/Users/someone");
        h.ViewModel.ErrorMessage.Should().NotContain(".db");
    }

    [Fact]
    public async Task BackupNowAsync_failure_message_does_not_leak_the_full_path_from_the_exception()
    {
        var h = Build(NewTempRoot());
        h.BackupService.Throws = new IOException(
            "/Users/someone/Library/Application Support/Tsumugi/tsumugi.db は使用中です");

        await h.ViewModel.BackupNowAsync();

        h.ViewModel.ErrorMessage.Should().NotBeNullOrEmpty();
        h.ViewModel.ErrorMessage.Should().NotContain("/Users/someone");
        h.ViewModel.ErrorMessage.Should().NotContain(".db");
    }

    [Fact]
    public async Task SaveCopyAsync_hands_the_backup_bytes_to_the_file_save_service()
    {
        var h = Build(NewTempRoot());

        await h.ViewModel.SaveCopyAsync();

        h.FileSave.Saved.Should().NotBeNull().And.NotBeEmpty();
        h.FileSave.SuggestedFileName.Should().Be("tsumugi-backup-20260816-173000.db");
    }

    [Fact]
    public async Task SaveCopyAsync_deletes_the_temporary_export_file_afterwards()
    {
        var h = Build(NewTempRoot());

        await h.ViewModel.SaveCopyAsync();

        Directory.GetFiles(h.BackupDirectoryPath, "*.export").Should().BeEmpty();

        // ADR 0052 が主張する「一時ファイルは保護ディレクトリ内に作られる」ことを固定する
        // （システムの一時ディレクトリは 0700 で保護されていないため使わない。最終レビュー指摘8）。
        h.BackupService.Destinations.Should().ContainSingle()
            .Which.Should().StartWith(h.BackupDirectoryPath);
    }
}
