using Tsumugi.Application.Abstractions;

namespace Tsumugi.Application.UseCases;

public sealed class BackupDatabaseUseCase(IBackupService backupService)
{
    public Task ExecuteAsync(string destinationPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("バックアップ先パスは必須です。", nameof(destinationPath));
        return backupService.BackupToAsync(destinationPath, ct);
    }
}
