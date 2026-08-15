using Tsumugi.Application.Abstractions;

namespace Tsumugi.Application.UseCases.Backup;

/// <summary>
/// 「控えを保存」用に、バックアップの中身をバイト列で返す。
///
/// 一時ファイルを**保護ディレクトリ内**に作ってから読み出し、直後に削除する。
/// システムの一時ディレクトリを使わないのは、そこが 0700 で保護されておらず、
/// 個人情報を含む DB の平文コピーを共有領域へ置くことになるため。
/// </summary>
public sealed class ExportBackupCopyUseCase(
    IDatabaseFileLocation location,
    IBackupService backupService,
    TimeProvider clock)
{
    public async Task<(string SuggestedFileName, byte[] Content)> ExecuteAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var fileName = Application.Backup.BackupFileName.Create(now);
        var staging = Path.Combine(location.BackupDirectory, fileName + ".export");

        try
        {
            await backupService.BackupToAsync(staging, ct);
            var content = await File.ReadAllBytesAsync(staging, ct);
            return (fileName, content);
        }
        finally
        {
            if (File.Exists(staging)) File.Delete(staging);
        }
    }
}
