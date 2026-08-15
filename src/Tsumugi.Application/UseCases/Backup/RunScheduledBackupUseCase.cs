using System.Globalization;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.UseCases.Backup;

/// <summary>
/// 終了時の自動バックアップ。バックアップ → 世代削除 → 監査 の順に実行する。
/// 設定は持たない（spec 決定3）。保存先も保持世代も固定。
/// </summary>
public sealed class RunScheduledBackupUseCase(
    IDatabaseFileLocation location,
    IBackupService backupService,
    IBackupDirectory backupDirectory,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public const string TargetType = "Database";

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var fileName = Application.Backup.BackupFileName.Create(now);
        var destination = Path.Combine(location.BackupDirectory, fileName);

        await backupService.BackupToAsync(destination, ct);

        var asOf = DateOnly.FromDateTime(now.UtcDateTime);
        var doomed = Application.Backup.BackupGenerationPolicy.SelectForDeletion(
            backupDirectory.ListFileNames(), asOf);
        foreach (var name in doomed) backupDirectory.Delete(name);

        // 監査にはファイル名だけを書く（固定ディレクトリなのでフルパスは不要。ハード制約4）。
        var summary = string.Create(
            CultureInfo.InvariantCulture,
            $"自動バックアップ {fileName}（世代削除 {doomed.Count} 件）");

        await auditTrail.RecordAsync(
            actor: "system", AuditAction.Backup, TargetType, Guid.Empty,
            occurredAt: now, summary, ct);

        await unitOfWork.SaveChangesAsync(ct);
    }
}
