using System.Globalization;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.UseCases.Backup;

/// <summary>
/// バックアップからの復元。監査 → 保存 → 退避 → 置換 の順（spec 決定5）。
///
/// 順序が仕様である理由: 置換すると現行 DB は失われるため、監査エントリを先に保存し、
/// その後に取る退避スナップショットへ記録ごと写す。逆順だと復元の事実が残らない。
/// </summary>
public sealed class RestoreDatabaseUseCase(
    IDatabaseFileLocation location,
    IBackupService backupService,
    IDatabaseRestoreService restoreService,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public const string TargetType = "Database";

    /// <param name="backupFileName">
    /// バックアップディレクトリ**直下のファイル名**（パスではない）。呼び出し側に保存先を知らせないため。
    /// </param>
    public async Task ExecuteAsync(string backupFileName, string actor, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        if (backupFileName != Path.GetFileName(backupFileName))
        {
            throw new ArgumentException(
                "バックアップディレクトリ直下のファイル名のみ指定できます。", nameof(backupFileName));
        }

        var now = clock.GetUtcNow();
        var snapshotName = Application.Backup.BackupFileName.CreatePreRestore(now);
        var sourceName = backupFileName;

        // 1. 監査（ファイル名のみ。フルパスは書かない。ハード制約4）
        var summary = string.Create(
            CultureInfo.InvariantCulture,
            $"復元 {sourceName} を適用（復元前退避 {snapshotName}）");
        await auditTrail.RecordAsync(
            actor, AuditAction.Restore, TargetType, Guid.Empty, now, summary, ct);

        // 2. 保存（ここまでが退避スナップショットに載る）
        await unitOfWork.SaveChangesAsync(ct);

        // 3. 退避（pre-restore 接頭辞は世代管理の対象外＝自動削除しない）
        await backupService.BackupToAsync(
            Path.Combine(location.BackupDirectory, snapshotName), ct);

        // 4. 置換
        await restoreService.RestoreFromAsync(
            Path.Combine(location.BackupDirectory, backupFileName), ct);
    }
}
