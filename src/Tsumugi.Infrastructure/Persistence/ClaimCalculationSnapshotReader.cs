using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Persistence;

/// <summary>
/// 事業所・サービス月の請求算定入力を単一のdeferred read transactionで取得する。
/// 各追記型revision chainはDomainの各Policyで実効値へ縮約し、履歴不正はそのまま例外を伝播する
/// （フェイルクローズ、<see cref="ClaimFinalizationStore"/> と同じtxパターン）。
/// </summary>
public sealed class ClaimCalculationSnapshotReader(
    IDbContextFactory<TsumugiDbContext> contextFactory,
    IOfficeClaimProfilePolicyProvider profilePolicyProvider) : IClaimCalculationSnapshotReader
{
    public async Task<ClaimCalculationSnapshot> ReadAsync(
        Guid officeId, ServiceMonth serviceMonth, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        await db.Database.OpenConnectionAsync(ct);
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        await using var transaction = connection.BeginTransaction(deferred: true);
        await db.Database.UseTransactionAsync(transaction, ct);
        try
        {
            var monthStart = new DateOnly(serviceMonth.Year, serviceMonth.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var effectiveClaimInputs = await ReadEffectiveClaimInputsAsync(
                db, officeId, serviceMonth, ct);
            var recipientIds = effectiveClaimInputs
                .Select(input => input.RecipientId)
                .Distinct()
                .ToArray();

            var billedDaysByRecipient = await ReadBilledDaysByRecipientAsync(
                db, recipientIds, monthStart, monthEnd, ct);
            var effectiveCertificateEvidences = await ReadEffectiveCertificateEvidencesAsync(
                db, recipientIds, monthEnd, ct);
            var effectiveAverageWageEvidences = await ReadEffectiveAverageWageEvidencesAsync(
                db, officeId, serviceMonth, ct);
            var profile = await ReadEffectiveProfileAsync(db, officeId, monthEnd, ct);

            return new ClaimCalculationSnapshot(
                profile,
                effectiveClaimInputs,
                effectiveCertificateEvidences,
                effectiveAverageWageEvidences,
                billedDaysByRecipient);
        }
        finally
        {
            await transaction.RollbackAsync(ct);
        }
    }

    private static async Task<IReadOnlyList<ClaimInput>> ReadEffectiveClaimInputsAsync(
        TsumugiDbContext db, Guid officeId, ServiceMonth serviceMonth, CancellationToken ct)
    {
        var history = await db.ClaimInputs.AsNoTracking()
            .Where(input => input.OfficeId == officeId && input.ServiceMonth == serviceMonth)
            .ToListAsync(ct);
        return history
            .GroupBy(input => input.RootId)
            .Select(group => ClaimInputPolicy.Effective(group.ToArray()))
            .Where(effective => effective is not null)
            .Select(effective => effective!)
            .OrderBy(input => input.RecipientId)
            .ToArray();
    }

    private static async Task<IReadOnlyDictionary<Guid, int>> ReadBilledDaysByRecipientAsync(
        TsumugiDbContext db,
        IReadOnlyCollection<Guid> recipientIds,
        DateOnly monthStart,
        DateOnly monthEnd,
        CancellationToken ct)
    {
        var result = new Dictionary<Guid, int>();
        foreach (var recipientId in recipientIds)
        {
            var dailyRecords = await db.DailyRecords.AsNoTracking()
                .Where(record => record.RecipientId == recipientId
                    && record.ServiceDate >= monthStart
                    && record.ServiceDate <= monthEnd)
                .ToListAsync(ct);
            var effectiveByDate = DailyRecordPolicy.EffectiveByDate(dailyRecords);
            result[recipientId] = effectiveByDate.Values
                .Count(record => record.Attendance == Attendance.Present);
        }
        return result;
    }

    private static async Task<IReadOnlyList<CertificateClaimEvidence>>
        ReadEffectiveCertificateEvidencesAsync(
            TsumugiDbContext db,
            IReadOnlyCollection<Guid> recipientIds,
            DateOnly monthEnd,
            CancellationToken ct)
    {
        var result = new List<CertificateClaimEvidence>();
        foreach (var recipientId in recipientIds)
        {
            var certificates = await db.Certificates.AsNoTracking()
                .Where(certificate => certificate.RecipientId == recipientId)
                .ToListAsync(ct);
            var effectiveCertificate = Tsumugi.Domain.Logic.Claim.CertificatePolicy.EffectiveVersion(
                certificates, monthEnd);
            if (effectiveCertificate is null) continue;

            var evidenceHistory = await db.CertificateClaimEvidences.AsNoTracking()
                .Where(evidence => evidence.CertificateId == effectiveCertificate.Id)
                .ToListAsync(ct);
            if (CertificateClaimEvidencePolicy.Effective(evidenceHistory) is { } effectiveEvidence)
                result.Add(effectiveEvidence);
        }
        return result;
    }

    private static async Task<IReadOnlyList<AverageWageAnnualEvidence>>
        ReadEffectiveAverageWageEvidencesAsync(
            TsumugiDbContext db, Guid officeId, ServiceMonth serviceMonth, CancellationToken ct)
    {
        var fiscalYear = serviceMonth.Month >= 4 ? serviceMonth.Year : serviceMonth.Year - 1;
        var history = await db.AverageWageAnnualEvidences.AsNoTracking()
            .Where(evidence => evidence.OfficeId == officeId && evidence.SourceFiscalYear == fiscalYear)
            .ToListAsync(ct);
        return history
            .GroupBy(evidence => evidence.RootId)
            .Select(group => AverageWageAnnualEvidencePolicy.Effective(group.ToArray()))
            .Where(effective => effective is not null)
            .Select(effective => effective!)
            .ToArray();
    }

    private async Task<OfficeClaimProfile?> ReadEffectiveProfileAsync(
        TsumugiDbContext db, Guid officeId, DateOnly monthEnd, CancellationToken ct)
    {
        var history = await db.OfficeClaimProfiles.AsNoTracking()
            .Where(profile => profile.OfficeId == officeId)
            .ToListAsync(ct);

        var candidates = new List<OfficeClaimProfile>();
        foreach (var group in history.GroupBy(profile => profile.RootId))
        {
            var items = group.ToArray();
            var latestNonCancel = items
                .Where(item => item.Kind != RecordKind.Cancel)
                .MaxBy(item => item.Revision);
            if (latestNonCancel?.MasterVersion is not { } masterVersion) continue;

            var policy = profilePolicyProvider.Resolve(masterVersion);
            if (policy.Effective(items) is { } effective && effective.EffectiveFrom <= monthEnd)
                candidates.Add(effective);
        }

        return candidates.Count == 0 ? null : candidates.MaxBy(profile => profile.EffectiveFrom);
    }
}
