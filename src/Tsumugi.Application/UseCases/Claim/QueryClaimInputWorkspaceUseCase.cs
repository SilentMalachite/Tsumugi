using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;

namespace Tsumugi.Application.UseCases.Claim;

public sealed class QueryClaimInputWorkspaceUseCase(
    IClaimInputRepository claimInputRepository,
    IAverageWageAnnualEvidenceRepository averageWageRepository,
    IOfficeClaimProfileRepository officeProfileRepository,
    ICertificateClaimEvidenceRepository certificateEvidenceRepository,
    IUpperLimitManagementStatementRepository statementRepository,
    IOfficeClaimProfilePolicyProvider officeProfilePolicyProvider)
{
    public async Task<ClaimInputWorkspaceDto> ExecuteAsync(
        QueryClaimInputWorkspaceRequest request,
        CancellationToken ct)
    {
        ClaimInputQueryGuard.Validate(request);

        var claimInputs = await claimInputRepository.ListHistoryAsync(
            request.OfficeId, request.RecipientId, request.ServiceMonth, ct);
        var averageWageEvidence = await averageWageRepository.ListHistoryAsync(
            request.OfficeId, request.SourceFiscalYear, ct);
        var officeProfiles = await officeProfileRepository.ListByOfficeAsync(
            request.OfficeId, ct);
        var certificateEvidence = await certificateEvidenceRepository.ListByCertificateAsync(
            request.CertificateId, ct);
        var statements = await statementRepository.ListHistoryAggregatesAsync(
            request.OfficeId, request.RecipientId, request.ServiceMonth, ct);

        return new ClaimInputWorkspaceDto(
            MapClaimInputChain(claimInputs),
            MapAverageWageChain(averageWageEvidence),
            MapOfficeProfileChains(officeProfiles),
            MapCertificateEvidenceChains(certificateEvidence),
            MapStatementChain(statements, request.CertificateId));
    }

    private static ClaimInputRevisionChainDto<ClaimInputQueryRevisionDto>? MapClaimInputChain(
        IReadOnlyList<ClaimInput> items)
    {
        var groups = GroupByRoot(items, item => item.RootId);
        if (groups.Length == 0) return null;
        ClaimInputQueryGuard.RequireSingleRoot(groups.Length);
        var history = groups[0].Items;
        ClaimInputQueryGuard.ValidateHistory(() => ClaimInputPolicy.ValidateHistory(history));
        return CreateChain(
            groups[0].RootId,
            history,
            item => item.Revision,
            item => item.Id,
            item => item.Kind,
            Map);
    }

    private static ClaimInputRevisionChainDto<AverageWageAnnualEvidenceQueryRevisionDto>?
        MapAverageWageChain(IReadOnlyList<AverageWageAnnualEvidence> items)
    {
        var groups = GroupByRoot(items, item => item.RootId);
        if (groups.Length == 0) return null;
        ClaimInputQueryGuard.RequireSingleRoot(groups.Length);
        var history = groups[0].Items;
        ClaimInputQueryGuard.ValidateHistory(
            () => AverageWageAnnualEvidencePolicy.ValidateHistory(history));
        return CreateChain(
            groups[0].RootId,
            history,
            item => item.Revision,
            item => item.Id,
            item => item.Kind,
            Map);
    }

    private ClaimInputRevisionChainDto<OfficeClaimProfileQueryRevisionDto>[]
        MapOfficeProfileChains(IReadOnlyList<OfficeClaimProfile> items) =>
        GroupByRoot(items, item => item.RootId)
            .Select(group =>
            {
                var latestNonCancel = group.Items
                    .Where(item => item.Kind != RecordKind.Cancel)
                    .MaxBy(item => item.Revision);
                if (latestNonCancel?.MasterVersion is not { } masterVersion)
                    throw new ClaimInputQueryException(ClaimInputQueryErrorCode.InvalidHistory);
                OfficeClaimProfilePolicy officeProfilePolicy;
                try
                {
                    officeProfilePolicy = officeProfilePolicyProvider.Resolve(masterVersion);
                }
                catch (ClaimMasterPolicyUnavailableException)
                {
                    throw new ClaimInputQueryException(
                        ClaimInputQueryErrorCode.MasterUnavailable);
                }

                ClaimInputQueryGuard.ValidateHistory(
                    () => officeProfilePolicy.ValidateHistory(group.Items));
                return CreateChain(
                    group.RootId,
                    group.Items,
                    item => item.Revision,
                    item => item.Id,
                    item => item.Kind,
                    Map);
            })
            .ToArray();

    private static ClaimInputRevisionChainDto<CertificateClaimEvidenceQueryRevisionDto>[]
        MapCertificateEvidenceChains(IReadOnlyList<CertificateClaimEvidence> items) =>
        GroupByRoot(items, item => item.RootId)
            .Select(group =>
            {
                ClaimInputQueryGuard.ValidateHistory(
                    () => CertificateClaimEvidencePolicy.ValidateHistory(group.Items));
                return CreateChain(
                    group.RootId,
                    group.Items,
                    item => item.Revision,
                    item => item.Id,
                    item => item.Kind,
                    Map);
            })
            .ToArray();

    private static ClaimInputRevisionChainDto<UpperLimitManagementStatementQueryRevisionDto>?
        MapStatementChain(
            IReadOnlyList<UpperLimitManagementStatementAggregate> aggregates,
            Guid certificateId)
    {
        var groups = aggregates
            .GroupBy(item => item.Header.RootId)
            .OrderBy(group => group.Key)
            .Select(group => new StatementGroup(group.Key, group.ToArray()))
            .ToArray();
        if (groups.Length == 0) return null;

        foreach (var group in groups)
        {
            var groupHistory = group.Aggregates.Select(item => item.Header).ToArray();
            var groupLines = group.Aggregates.SelectMany(item => item.Lines).ToArray();
            ClaimInputQueryGuard.ValidateHistory(
                () => UpperLimitManagementStatementPolicy.ValidateHistory(
                    groupHistory, groupLines));
        }

        var candidates = groups
            .Where(group => group.Aggregates[0].Header.CertificateId == certificateId)
            .ToArray();
        if (candidates.Length == 0) return null;
        ClaimInputQueryGuard.RequireSingleRoot(candidates.Length);
        var selected = candidates[0];
        var history = selected.Aggregates.Select(item => item.Header).ToArray();
        var linesByStatement = selected.Aggregates.ToDictionary(
            item => item.Header.Id,
            item => item.Lines);
        return CreateChain(
            selected.RootId,
            history,
            item => item.Revision,
            item => item.Id,
            item => item.Kind,
            item => Map(item, linesByStatement[item.Id]));
    }

    private static ClaimInputRevisionChainDto<TDto> CreateChain<TEntity, TDto>(
        Guid rootId,
        IReadOnlyCollection<TEntity> items,
        Func<TEntity, int> revision,
        Func<TEntity, Guid> id,
        Func<TEntity, RecordKind> kind,
        Func<TEntity, TDto> map)
    {
        var ordered = items.OrderBy(revision).ToArray();
        var head = ordered[^1];
        var currentHeadId = id(head);
        return new ClaimInputRevisionChainDto<TDto>(
            rootId,
            currentHeadId,
            kind(head) == RecordKind.Cancel ? null : currentHeadId,
            ordered.Select(map).ToArray());
    }

    private static RootGroup<T>[] GroupByRoot<T>(
        IEnumerable<T> items,
        Func<T, Guid> rootId) =>
        items.GroupBy(rootId)
            .OrderBy(group => group.Key)
            .Select(group => new RootGroup<T>(group.Key, group.ToArray()))
            .ToArray();

    private static ClaimInputQueryRevisionDto Map(ClaimInput item) =>
        new(
            item.Id,
            item.OfficeId,
            item.RecipientId,
            item.ServiceMonth,
            item.RootId,
            item.Revision,
            item.Kind,
            item.ExpectedHeadId,
            item.UpperLimitManagementResult,
            item.UpperLimitManagedAmountYen,
            item.MunicipalSubsidyAmountYen,
            item.ExceptionalUsageStartMonth,
            item.ExceptionalUsageEndMonth,
            item.ExceptionalUsageDays,
            item.StandardUsageDayTotal,
            item.CreatedAt,
            item.CreatedBy);

    private static AverageWageAnnualEvidenceQueryRevisionDto Map(
        AverageWageAnnualEvidence item) =>
        new(
            item.Id,
            item.OfficeId,
            item.SourceFiscalYear,
            item.PeriodStart,
            item.PeriodEnd,
            item.RootId,
            item.Revision,
            item.Kind,
            item.ExpectedHeadId,
            item.AnnualWagePaidYen,
            item.AnnualExtendedUsers,
            item.AnnualOpeningDays,
            item.Completeness,
            item.EvidenceDocumentId,
            item.DailyEvidenceReference,
            item.MonthlyEvidenceReference,
            item.ConfirmedAt,
            item.ConfirmedBy,
            item.ConfirmationReason,
            item.CreatedAt,
            item.CreatedBy,
            ComputedAverageWageMonthYen(item));

    /// <summary>
    /// Task 13 (ADR 0023): 確認済み（<c>Complete</c>）の前年度実績revisionにだけ正式式の
    /// 平均工賃月額を付す。実績が不完全・取消のときは推測せず<c>null</c>。区分導出は
    /// PaymentBand数値境界マスタが未実装のため行わない（docs/open-questions.md）。
    /// </summary>
    private static int? ComputedAverageWageMonthYen(AverageWageAnnualEvidence item)
    {
        if (item.Completeness != FiscalYearCompleteness.Complete
            || item.AnnualWagePaidYen is not { } annualWagePaidYen
            || item.AnnualExtendedUsers is not { } annualExtendedUsers
            || item.AnnualOpeningDays is not { } annualOpeningDays
            || annualWagePaidYen < 0
            || annualExtendedUsers <= 0
            || annualOpeningDays <= 0)
        {
            return null;
        }

        return AverageWageFormula.Calculate(
            annualWagePaidYen, annualExtendedUsers, annualOpeningDays);
    }

    private static OfficeClaimProfileQueryRevisionDto Map(OfficeClaimProfile item) =>
        new(
            item.Id,
            item.OfficeId,
            item.EffectiveFrom,
            item.EffectiveTo,
            item.RootId,
            item.Revision,
            item.Kind,
            item.ExpectedHeadId,
            item.MasterVersion,
            item.ReformStatus,
            item.AverageWageBandOption,
            item.DesignationDate,
            item.SupportStartDate,
            item.EarlierRegisteredBandOption,
            item.EarlierRegistrationMonth,
            item.LaterRegisteredBandOption,
            item.LaterRegistrationMonth,
            item.ReformComparisonEvidenceDocumentId,
            item.FiledTransitionPeriod,
            item.FiledTransitionEvidenceDocumentId,
            item.EvidenceDocumentId,
            item.ConfirmedAt,
            item.ConfirmedBy,
            item.ConfirmationReason,
            item.CreatedAt,
            item.CreatedBy,
            item.CapacityHeadcount,
            item.StaffingKey,
            item.RegionKey);

    private static CertificateClaimEvidenceQueryRevisionDto Map(
        CertificateClaimEvidence item) =>
        new(
            item.Id,
            item.CertificateId,
            item.Validity,
            item.RootId,
            item.Revision,
            item.Kind,
            item.ExpectedHeadId,
            item.MonthlyCostCap,
            item.UpperLimitManagementApplicability,
            item.UpperLimitManagementOfficeNumber,
            item.Article31Status,
            item.Article31AmountYen,
            item.Article31EffectivePeriod,
            item.OriginalDocumentReference,
            item.ConfirmedAt,
            item.ConfirmedBy,
            item.ConfirmationReason,
            item.CreatedAt,
            item.CreatedBy);

    private static UpperLimitManagementStatementQueryRevisionDto Map(
        UpperLimitManagementStatement item,
        IReadOnlyList<UpperLimitManagementStatementLine> lines) =>
        new(
            item.Id,
            item.RootId,
            item.Revision,
            item.Kind,
            item.ExpectedHeadId,
            item.ServiceMonth,
            item.RecipientId,
            item.CertificateId,
            item.ManagingOfficeId,
            item.MunicipalityNumber,
            item.CertificateNumber,
            item.CertificateMonthlyCostCap,
            item.UpperLimitManagementApplicability,
            item.CertificateManagingOfficeNumber,
            item.ManagingOfficeNumber,
            item.ManagingOfficeName,
            item.OriginalCreationKind,
            item.ReceivedAt,
            item.OriginalDocumentReference,
            item.IsConfirmed,
            item.ConfirmedAt,
            item.ConfirmedBy,
            item.ConfirmationReason,
            item.Result,
            item.TotalCostYen,
            item.TotalPreManagementBurdenYen,
            item.TotalManagedBurdenYen,
            item.CreatedAt,
            item.CreatedBy,
            lines.OrderBy(line => line.LineNumber).Select(Map).ToArray());

    private static UpperLimitManagementStatementLineQueryDto Map(
        UpperLimitManagementStatementLine item) =>
        new(
            item.Id,
            item.StatementId,
            item.LineNumber,
            item.OfficeNumber,
            item.OfficeName,
            item.TotalCostYen,
            item.PreManagementBurdenYen,
            item.ManagedBurdenYen,
            item.CreatedAt,
            item.CreatedBy);

    private sealed record RootGroup<T>(Guid RootId, T[] Items);
    private sealed record StatementGroup(
        Guid RootId,
        UpperLimitManagementStatementAggregate[] Aggregates);
}

internal static class ClaimInputQueryGuard
{
    public static void Validate(QueryClaimInputWorkspaceRequest? request)
    {
        if (request is null
            || request.OfficeId == Guid.Empty
            || request.RecipientId == Guid.Empty
            || request.CertificateId == Guid.Empty
            || request.SourceFiscalYear is < 1900 or > 2199
            || !IsValid(request.ServiceMonth))
        {
            throw new ClaimInputQueryException(ClaimInputQueryErrorCode.InvalidRequest);
        }
    }

    public static void Validate(QueryIntensiveSupportEpisodeRequest? request)
    {
        if (request is null
            || request.OfficeId == Guid.Empty
            || request.RecipientId == Guid.Empty)
        {
            throw new ClaimInputQueryException(ClaimInputQueryErrorCode.InvalidRequest);
        }
    }

    public static void RequireSingleRoot(int count)
    {
        if (count != 1)
        {
            throw new ClaimInputQueryException(ClaimInputQueryErrorCode.InvalidHistory);
        }
    }

    public static void ValidateHistory(Action validate)
    {
        try
        {
            validate();
        }
        catch (InvalidOperationException)
        {
            throw new ClaimInputQueryException(ClaimInputQueryErrorCode.InvalidHistory);
        }
        catch (ArgumentException)
        {
            throw new ClaimInputQueryException(ClaimInputQueryErrorCode.InvalidHistory);
        }
    }

    private static bool IsValid(Domain.ValueObjects.ServiceMonth serviceMonth)
    {
        try
        {
            _ = serviceMonth.ToInt();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
