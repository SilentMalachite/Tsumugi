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
/// <remarks>
/// 対象利用者の範囲決定は、ClaimInputの入力有無だけに依存しない（Phase 2
/// <c>CalculateWagesUseCase</c> の前例を踏襲: ClaimInput未入力でも出席実績や契約があれば
/// 対象者として可視化し、請求漏れを黙らせない）。<c>Contract</c> は事業所IDを持たないため、
/// Phase 2 と同じく契約実効性のみで判定する（単一事業所運用前提。複数事業所対応は
/// Contract整理時に再実装 — open-questions既出）。
/// </remarks>
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

            // Task 9c: ContractedProviderの「本事業所行」判定にOfficeNumberが要るため、
            // 同一tx内で事業所も読む（既存のIOfficeRepository経由の読み取りとは別読取で、
            // 用途もこの一致判定に限定する）。
            var office = await db.Offices.AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == officeId, ct);

            var effectiveClaimInputs = await ReadEffectiveClaimInputsAsync(
                db, officeId, serviceMonth, ct);

            // 対象利用者の範囲 = 「当月1日時点で実効な契約」「当月に実効なPresent日次記録」
            // 「当月の実効ClaimInput」の和集合。ClaimInputだけを起点にすると、出席実績はあるのに
            // ClaimInput未入力の利用者が請求対象から黙って消える（サイレント過小請求）ため、
            // 3ソースいずれかの根拠があれば対象者として可視化する。
            var recipientIdsFromContract = await ReadRecipientsWithEffectiveContractAsync(
                db, monthStart, ct);
            var recipientIdsFromDailyRecords = await ReadRecipientsWithPresentDailyRecordAsync(
                db, monthStart, monthEnd, ct);
            var recipientIds = recipientIdsFromContract
                .Concat(recipientIdsFromDailyRecords)
                .Concat(effectiveClaimInputs.Select(input => input.RecipientId))
                .Distinct()
                .OrderBy(id => id)
                .ToArray();

            var (billedDaysByRecipient, dailyRecordAggregateByRecipient) =
                await ReadDailyRecordDataByRecipientAsync(db, recipientIds, monthStart, monthEnd, ct);
            var (certificateCountByRecipient, effectiveCertificateEvidenceByRecipient,
                    effectiveCertificateByRecipient) =
                await ReadEffectiveCertificateEvidencesAsync(db, recipientIds, monthStart, monthEnd, ct);
            var effectiveContractedProviderByRecipient = await ReadEffectiveContractedProvidersByRecipientAsync(
                db, effectiveCertificateByRecipient, office?.OfficeNumber, monthStart, monthEnd, ct);
            var effectiveAverageWageEvidences = await ReadEffectiveAverageWageEvidencesAsync(
                db, officeId, serviceMonth, ct);
            var profile = await ReadEffectiveProfileAsync(db, officeId, monthEnd, ct);
            var intensiveSupportEpisodeStartDateByRecipient =
                await ReadIntensiveSupportEpisodeStartDatesByRecipientAsync(db, officeId, recipientIds, ct);

            // Task 11: 体制届（加算の体制条件・ADR 0021）。実効1件の選定はDomainの
            // OfficeCapabilityPolicyへ委譲するため、ここでは同一tx内の全件読取に徹する
            // （DateRangeはJSON列のためSQLレベルで期間フィルタできない。Contractと同じ制約）。
            var officeCapabilities = await db.OfficeCapabilities.AsNoTracking()
                .Where(capability => capability.OfficeId == officeId)
                .ToListAsync(ct);

            return new ClaimCalculationSnapshot(
                recipientIds,
                profile,
                effectiveClaimInputs,
                effectiveCertificateEvidenceByRecipient,
                effectiveAverageWageEvidences,
                billedDaysByRecipient,
                certificateCountByRecipient,
                effectiveCertificateByRecipient,
                effectiveContractedProviderByRecipient,
                dailyRecordAggregateByRecipient,
                intensiveSupportEpisodeStartDateByRecipient,
                officeCapabilities);
        }
        finally
        {
            await transaction.RollbackAsync(ct);
        }
    }

    private static async Task<IReadOnlyList<Guid>> ReadRecipientsWithEffectiveContractAsync(
        TsumugiDbContext db, DateOnly anchor, CancellationToken ct)
    {
        // Contract に OfficeId はないため事業所では絞り込めない（Phase 2 CalculateWagesUseCase と
        // 同じ制約・同じ前提）。非アーカイブ利用者のうち、当月1日時点で実効な契約を持つ者を対象とする。
        var nonArchivedRecipientIds = await db.Recipients.AsNoTracking()
            .Where(recipient => recipient.ArchivedAt == null)
            .Select(recipient => recipient.Id)
            .ToListAsync(ct);
        if (nonArchivedRecipientIds.Count == 0) return Array.Empty<Guid>();

        var nonArchivedSet = nonArchivedRecipientIds.ToHashSet();
        // DateRange はJSON列のためSQLレベルでフィルタできない（ContractRepositoryと同じ理由）。
        var contracts = await db.Contracts.AsNoTracking().ToListAsync(ct);
        return contracts
            .Where(contract => nonArchivedSet.Contains(contract.RecipientId)
                && contract.Period.Contains(anchor))
            .Select(contract => contract.RecipientId)
            .Distinct()
            .ToArray();
    }

    private static async Task<IReadOnlyList<Guid>> ReadRecipientsWithPresentDailyRecordAsync(
        TsumugiDbContext db, DateOnly monthStart, DateOnly monthEnd, CancellationToken ct)
    {
        var records = await db.DailyRecords.AsNoTracking()
            .Where(record => record.ServiceDate >= monthStart && record.ServiceDate <= monthEnd)
            .ToListAsync(ct);

        var result = new List<Guid>();
        foreach (var group in records.GroupBy(record => record.RecipientId))
        {
            var effectiveByDate = DailyRecordPolicy.EffectiveByDate(group);
            if (effectiveByDate.Values.Any(record => record.Attendance == Attendance.Present))
                result.Add(group.Key);
        }
        return result;
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

    /// <summary>
    /// 出席日数と<see cref="ClaimDailyRecordAggregate"/>を同じ実効化走査から導出する
    /// （DailyRecordの月次クエリと訂正・取消縮約を1回で共用し、二重取得しない）。
    /// </summary>
    private static async Task<(
        IReadOnlyDictionary<Guid, int> BilledDaysByRecipient,
        IReadOnlyDictionary<Guid, ClaimDailyRecordAggregate> AggregateByRecipient)>
        ReadDailyRecordDataByRecipientAsync(
            TsumugiDbContext db,
            IReadOnlyCollection<Guid> recipientIds,
            DateOnly monthStart,
            DateOnly monthEnd,
            CancellationToken ct)
    {
        var billedDaysByRecipient = new Dictionary<Guid, int>();
        var aggregateByRecipient = new Dictionary<Guid, ClaimDailyRecordAggregate>();
        foreach (var recipientId in recipientIds)
        {
            var dailyRecords = await db.DailyRecords.AsNoTracking()
                .Where(record => record.RecipientId == recipientId
                    && record.ServiceDate >= monthStart
                    && record.ServiceDate <= monthEnd)
                .ToListAsync(ct);
            var effectiveByDate = DailyRecordPolicy.EffectiveByDate(dailyRecords);
            var presentDays = effectiveByDate.Values
                .Where(record => record.Attendance == Attendance.Present)
                .OrderBy(record => record.ServiceDate)
                .ToArray();

            billedDaysByRecipient[recipientId] = presentDays.Length;
            aggregateByRecipient[recipientId] = presentDays.Length == 0
                ? ClaimDailyRecordAggregate.Empty
                : new ClaimDailyRecordAggregate(
                    ServiceStartTime: presentDays
                        .Select(record => record.ServiceStartTime)
                        .FirstOrDefault(value => value is not null),
                    ServiceEndTime: presentDays
                        .Select(record => record.ServiceEndTime)
                        .FirstOrDefault(value => value is not null),
                    SpecialVisitSupportMinutesTotal: presentDays
                        .Sum(record => record.SpecialVisitSupportMinutes ?? 0),
                    OffsiteSupportApplied: presentDays.Any(record => record.OffsiteSupportApplied == true),
                    MedicalCoordinationType: presentDays
                        .Select(record => record.MedicalCoordinationType)
                        .FirstOrDefault(value => value != MedicalCoordinationType.Unspecified),
                    TrialUseSupportType: presentDays
                        .Select(record => record.TrialUseSupportType)
                        .FirstOrDefault(value => value != TrialUseSupportType.Unspecified),
                    RegionalCollaborationApplied: presentDays
                        .Any(record => record.RegionalCollaborationApplied == true),
                    IntensiveSupportApplied: presentDays.Any(record => record.IntensiveSupportApplied == true),
                    EmergencyAdmissionApplied: presentDays
                        .Any(record => record.EmergencyAdmissionApplied == true));
        }
        return (billedDaysByRecipient, aggregateByRecipient);
    }

    /// <summary>
    /// 受給者証rootの数（サービス月と有効期間が重なるもの）と、ちょうど1件のときだけの実効根拠を
    /// 利用者ごとに解決する。0件・2件以上（月途中の証切替を含む）は代表1件を黙って選ばず、
    /// 件数のみを返す。根拠は利用者IDを鍵とする辞書で明示的に対応付ける（位置対応ではない）。
    /// 判定はTask 9のreadiness gate側の責務。
    /// </summary>
    private static async Task<(
        IReadOnlyDictionary<Guid, int> CountByRecipient,
        IReadOnlyDictionary<Guid, CertificateClaimEvidence> EvidenceByRecipient,
        IReadOnlyDictionary<Guid, Certificate> CertificateByRecipient)>
        ReadEffectiveCertificateEvidencesAsync(
            TsumugiDbContext db,
            IReadOnlyCollection<Guid> recipientIds,
            DateOnly monthStart,
            DateOnly monthEnd,
            CancellationToken ct)
    {
        var countByRecipient = new Dictionary<Guid, int>();
        var evidenceByRecipient = new Dictionary<Guid, CertificateClaimEvidence>();
        // Task 9c: Certificate.MunicipalityNumber等の写像用に、実効受給者証がちょうど1件のときだけ
        // その実体も利用者IDを鍵として保持する（evidenceByRecipientと同じ規約）。
        var certificateByRecipient = new Dictionary<Guid, Certificate>();
        foreach (var recipientId in recipientIds)
        {
            var certificates = await db.Certificates.AsNoTracking()
                .Where(certificate => certificate.RecipientId == recipientId)
                .ToListAsync(ct);

            var overlapping = ResolveOverlappingCertificates(certificates, monthStart, monthEnd);
            countByRecipient[recipientId] = overlapping.Count;
            if (overlapping.Count != 1) continue;

            certificateByRecipient[recipientId] = overlapping[0];

            var evidenceHistory = await db.CertificateClaimEvidences.AsNoTracking()
                .Where(evidence => evidence.CertificateId == overlapping[0].Id)
                .ToListAsync(ct);
            if (CertificateClaimEvidencePolicy.Effective(evidenceHistory) is { } effectiveEvidence)
                evidenceByRecipient[recipientId] = effectiveEvidence;
        }
        return (countByRecipient, evidenceByRecipient, certificateByRecipient);
    }

    /// <summary>
    /// Task 9c: <see cref="ContractedProvider.CertificateEntryNumber"/>写像用に、実効受給者証の
    /// 「サービス事業者記入欄」から本事業所（<c>ProviderNumber == officeNumber</c>）かつ
    /// サービス月と契約期間が重なる行を解決する。0件・2件以上（契約行の重複）は代表を選ばず、
    /// エントリを作らない（fail-closed。判定は請求readiness gate側の責務）。
    /// 注意: <see cref="ContractedProvider"/> は登録時点の <see cref="Certificate.Id"/> に紐づくため、
    /// 受給者証を訂正すると新headのIdとは一致しなくなり、本readerでは解決されない
    /// （readinessが <see cref="ContractedProvider.CertificateEntryNumber"/> 不足として算定不能にする=フェイルクローズ）。
    /// 挙動と再登録手順は open-questions #36 および CertificateViewModel の staleness 表示を参照。
    /// </summary>
    private static async Task<IReadOnlyDictionary<Guid, ContractedProvider>>
        ReadEffectiveContractedProvidersByRecipientAsync(
            TsumugiDbContext db,
            IReadOnlyDictionary<Guid, Certificate> certificateByRecipient,
            string? officeNumber,
            DateOnly monthStart,
            DateOnly monthEnd,
            CancellationToken ct)
    {
        var result = new Dictionary<Guid, ContractedProvider>();
        if (string.IsNullOrWhiteSpace(officeNumber)) return result;

        foreach (var (recipientId, certificate) in certificateByRecipient)
        {
            var candidates = await db.ContractedProviders.AsNoTracking()
                .Where(provider => provider.CertificateId == certificate.Id
                    && provider.ProviderNumber == officeNumber)
                .ToListAsync(ct);

            var overlapping = candidates
                .Where(provider => provider.ContractDate <= monthEnd
                    && (provider.TerminationDate == null || provider.TerminationDate >= monthStart))
                .ToArray();
            if (overlapping.Length == 1)
                result[recipientId] = overlapping[0];
        }
        return result;
    }

    /// <summary>
    /// Task 9c: <see cref="IntensiveSupportEpisode"/>の追記型revision chainを
    /// <see cref="IntensiveSupportEpisodePolicy.Effective"/>で縮約し、取消でない実効エピソードが
    /// 存在する利用者だけを鍵に開始日を保持する（サービス月による絞込みは行わない。集中的支援開始日は
    /// 特定月のスナップショットではなく継続的な状態のため）。
    /// </summary>
    private static async Task<IReadOnlyDictionary<Guid, DateOnly>>
        ReadIntensiveSupportEpisodeStartDatesByRecipientAsync(
            TsumugiDbContext db,
            Guid officeId,
            IReadOnlyCollection<Guid> recipientIds,
            CancellationToken ct)
    {
        var result = new Dictionary<Guid, DateOnly>();
        foreach (var recipientId in recipientIds)
        {
            var history = await db.IntensiveSupportEpisodes.AsNoTracking()
                .Where(episode => episode.OfficeId == officeId && episode.RecipientId == recipientId)
                .ToListAsync(ct);
            if (history.Count == 0) continue;

            if (IntensiveSupportEpisodePolicy.Effective(history) is { StartDate: { } startDate })
                result[recipientId] = startDate;
        }
        return result;
    }

    /// <summary>
    /// サービス月内の暦日と有効期間が重なる受給者証rootを収集する。
    /// Domainを変更せず、既存の <c>CertificatePolicy.EffectiveVersion</c>（1日粒度の実効判定＋
    /// 履歴整合性検証）を月内の全暦日に対して走査し、返された実効版の重複排除集合として
    /// 月次overlap判定へ委譲する（DateRange.Overlapsと等価: 月内のいずれか1日でも
    /// Validity.Containsが真になるrootは月と重なる）。同一日に複数rootが実効となる不正データは
    /// EffectiveVersion側で例外化され、そのままフェイルクローズで伝播する。
    /// </summary>
    private static List<Certificate> ResolveOverlappingCertificates(
        IReadOnlyList<Certificate> certificates, DateOnly monthStart, DateOnly monthEnd)
    {
        var overlapping = new List<Certificate>();
        for (var day = monthStart; day <= monthEnd; day = day.AddDays(1))
        {
            if (Tsumugi.Domain.Logic.Claim.CertificatePolicy.EffectiveVersion(certificates, day)
                    is { } effectiveOnDay
                && overlapping.All(certificate => certificate.Id != effectiveOnDay.Id))
            {
                overlapping.Add(effectiveOnDay);
            }
        }
        return overlapping;
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
