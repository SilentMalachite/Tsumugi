namespace Tsumugi.Application.Abstractions;

public interface IBackupService
{
    Task BackupToAsync(string destinationPath, CancellationToken ct);
}
