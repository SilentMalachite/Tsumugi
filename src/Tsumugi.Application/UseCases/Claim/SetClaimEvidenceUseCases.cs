using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;

namespace Tsumugi.Application.UseCases.Claim;

public sealed class SetAverageWageAnnualEvidenceUseCase(
    IAverageWageAnnualEvidenceRepository repo,
    IUnitOfWork uow,
    TimeProvider clock)
{
    public async Task<ClaimInputRevisionDto> ExecuteAsync(
        SetAverageWageAnnualEvidenceRequest request,
        string actor,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ClaimInputSaveGuard.ValidateActor(actor);
        ClaimInputSaveGuard.ValidateIdentities(request.OfficeId);

        var candidates = await repo.ListHistoryAsync(
            request.OfficeId, request.SourceFiscalYear, ct);
        var history = candidates
            .Where(item => item.PeriodStart == request.PeriodStart
                           && item.PeriodEnd == request.PeriodEnd)
            .ToArray();
        ClaimInputSaveGuard.ValidateExistingHistory(
            () => AverageWageAnnualEvidencePolicy.ValidateHistory(history));
        var head = history.MaxBy(item => item.Revision);
        ClaimInputSaveGuard.ValidateRequestedRevision(
            request.Kind, request.ExpectedHeadId, head?.Id);

        var id = Guid.NewGuid();
        var entity = new AverageWageAnnualEvidence
        {
            Id = id,
            OfficeId = request.OfficeId,
            SourceFiscalYear = request.SourceFiscalYear,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            RootId = head?.RootId ?? id,
            Revision = AverageWageAnnualEvidencePolicy.NextRevision(history),
            Kind = request.Kind,
            ExpectedHeadId = request.ExpectedHeadId,
            AnnualWagePaidYen = request.AnnualWagePaidYen,
            AnnualExtendedUsers = request.AnnualExtendedUsers,
            AnnualOpeningDays = request.AnnualOpeningDays,
            Completeness = request.Completeness,
            EvidenceDocumentId = request.EvidenceDocumentId,
            DailyEvidenceReference = request.DailyEvidenceReference,
            MonthlyEvidenceReference = request.MonthlyEvidenceReference,
            ConfirmedAt = request.ConfirmedAt,
            ConfirmedBy = request.ConfirmedBy,
            ConfirmationReason = request.ConfirmationReason,
            CreatedAt = clock.GetUtcNow(),
            CreatedBy = actor,
            ConcurrencyToken = Guid.NewGuid(),
        };

        ClaimInputSaveGuard.ValidateCandidate(
            () => AverageWageAnnualEvidencePolicy.ValidateHistory([.. history, entity]));
        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return ClaimInputRevisionMapper.Map(entity, entity.RootId, entity.Revision,
            entity.Kind, entity.ExpectedHeadId);
    }
}

public sealed class SetOfficeClaimProfileUseCase(
    IOfficeClaimProfileRepository repo,
    IUnitOfWork uow,
    TimeProvider clock,
    IOfficeClaimProfilePolicyProvider policyProvider)
{
    public async Task<ClaimInputRevisionDto> ExecuteAsync(
        SetOfficeClaimProfileRequest request,
        string actor,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ClaimInputSaveGuard.ValidateActor(actor);
        ClaimInputSaveGuard.ValidateIdentities(request.OfficeId);
        if (request.Kind != RecordKind.Cancel && request.MasterVersion is null)
        {
            throw new ClaimInputSaveException(
                ClaimInputSaveErrorCode.InvalidRequest,
                ClaimInputFieldCode.Values);
        }

        var candidates = await repo.ListByOfficeAsync(request.OfficeId, ct);
        var history = candidates
            .Where(item => item.EffectiveFrom == request.EffectiveFrom
                           && item.EffectiveTo == request.EffectiveTo)
            .ToArray();
        ClaimMasterVersion masterVersion;
        if (request.Kind == RecordKind.Cancel)
        {
            var latestNonCancel = history
                .Where(item => item.Kind != RecordKind.Cancel)
                .MaxBy(item => item.Revision);
            if (latestNonCancel?.MasterVersion is not { } historyMasterVersion)
            {
                throw new ClaimInputSaveException(
                    ClaimInputSaveErrorCode.InvalidHistory,
                    ClaimInputFieldCode.History);
            }

            masterVersion = historyMasterVersion;
        }
        else
        {
            masterVersion = request.MasterVersion!.Value;
        }

        OfficeClaimProfilePolicy policy;
        try
        {
            policy = policyProvider.Resolve(masterVersion);
        }
        catch (ClaimMasterPolicyUnavailableException)
        {
            throw new ClaimInputSaveException(
                ClaimInputSaveErrorCode.MasterUnavailable,
                ClaimInputFieldCode.Values);
        }

        ClaimInputSaveGuard.ValidateExistingHistory(() => policy.ValidateHistory(history));
        var head = history.MaxBy(item => item.Revision);
        ClaimInputSaveGuard.ValidateRequestedRevision(
            request.Kind, request.ExpectedHeadId, head?.Id);

        var id = Guid.NewGuid();
        var entity = new OfficeClaimProfile
        {
            Id = id,
            OfficeId = request.OfficeId,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            RootId = head?.RootId ?? id,
            Revision = policy.NextRevision(history),
            Kind = request.Kind,
            ExpectedHeadId = request.ExpectedHeadId,
            MasterVersion = request.MasterVersion,
            ReformStatus = request.ReformStatus,
            AverageWageBandOption = request.AverageWageBandOption,
            DesignationDate = request.DesignationDate,
            SupportStartDate = request.SupportStartDate,
            EarlierRegisteredBandOption = request.EarlierRegisteredBandOption,
            EarlierRegistrationMonth = request.EarlierRegistrationMonth,
            LaterRegisteredBandOption = request.LaterRegisteredBandOption,
            LaterRegistrationMonth = request.LaterRegistrationMonth,
            ReformComparisonEvidenceDocumentId = request.ReformComparisonEvidenceDocumentId,
            FiledTransitionPeriod = request.FiledTransitionPeriod,
            FiledTransitionEvidenceDocumentId = request.FiledTransitionEvidenceDocumentId,
            EvidenceDocumentId = request.EvidenceDocumentId,
            ConfirmedAt = request.ConfirmedAt,
            ConfirmedBy = request.ConfirmedBy,
            ConfirmationReason = request.ConfirmationReason,
            CapacityHeadcount = request.CapacityHeadcount,
            StaffingKey = request.StaffingKey,
            RegionKey = request.RegionKey,
            CreatedAt = clock.GetUtcNow(),
            CreatedBy = actor,
            ConcurrencyToken = Guid.NewGuid(),
        };

        ClaimInputSaveGuard.ValidateCandidate(() => policy.ValidateHistory([.. history, entity]));
        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return ClaimInputRevisionMapper.Map(entity, entity.RootId, entity.Revision,
            entity.Kind, entity.ExpectedHeadId);
    }
}

public sealed class SetCertificateClaimEvidenceUseCase(
    ICertificateClaimEvidenceRepository repo,
    IUnitOfWork uow,
    TimeProvider clock)
{
    public async Task<ClaimInputRevisionDto> ExecuteAsync(
        SetCertificateClaimEvidenceRequest request,
        string actor,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ClaimInputSaveGuard.ValidateActor(actor);
        ClaimInputSaveGuard.ValidateIdentities(request.CertificateId);

        var candidates = await repo.ListByCertificateAsync(request.CertificateId, ct);
        var history = candidates
            .Where(item => item.Validity == request.Validity)
            .ToArray();
        ClaimInputSaveGuard.ValidateExistingHistory(
            () => CertificateClaimEvidencePolicy.ValidateHistory(history));
        var head = history.MaxBy(item => item.Revision);
        ClaimInputSaveGuard.ValidateRequestedRevision(
            request.Kind, request.ExpectedHeadId, head?.Id);

        var id = Guid.NewGuid();
        var entity = new CertificateClaimEvidence
        {
            Id = id,
            CertificateId = request.CertificateId,
            Validity = request.Validity,
            RootId = head?.RootId ?? id,
            Revision = CertificateClaimEvidencePolicy.NextRevision(history),
            Kind = request.Kind,
            ExpectedHeadId = request.ExpectedHeadId,
            MonthlyCostCap = request.MonthlyCostCap,
            UpperLimitManagementApplicability = request.UpperLimitManagementApplicability,
            UpperLimitManagementOfficeNumber = request.UpperLimitManagementOfficeNumber,
            Article31Status = request.Article31Status,
            Article31AmountYen = request.Article31AmountYen,
            Article31EffectivePeriod = request.Article31EffectivePeriod,
            OriginalDocumentReference = request.OriginalDocumentReference,
            ConfirmedAt = request.ConfirmedAt,
            ConfirmedBy = request.ConfirmedBy,
            ConfirmationReason = request.ConfirmationReason,
            CreatedAt = clock.GetUtcNow(),
            CreatedBy = actor,
            ConcurrencyToken = Guid.NewGuid(),
        };

        ClaimInputSaveGuard.ValidateCandidate(
            () => CertificateClaimEvidencePolicy.ValidateHistory([.. history, entity]));
        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return ClaimInputRevisionMapper.Map(entity, entity.RootId, entity.Revision,
            entity.Kind, entity.ExpectedHeadId);
    }
}

public sealed class SetUpperLimitManagementStatementUseCase(
    IUpperLimitManagementStatementRepository repo,
    IUnitOfWork uow,
    TimeProvider clock)
{
    public async Task<ClaimInputRevisionDto> ExecuteAsync(
        SetUpperLimitManagementStatementRequest request,
        string actor,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ClaimInputSaveGuard.ValidateActor(actor);
        ClaimInputSaveGuard.ValidateIdentities(
            request.RecipientId, request.CertificateId, request.ManagingOfficeId);

        var candidates = await repo.ListHistoryAggregatesAsync(
            request.ManagingOfficeId, request.RecipientId, request.ServiceMonth, ct);
        var aggregates = candidates
            .Where(item => item.Header.CertificateId == request.CertificateId)
            .ToArray();
        var history = aggregates.Select(item => item.Header).ToArray();
        var historyLines = aggregates.SelectMany(item => item.Lines).ToArray();
        ClaimInputSaveGuard.ValidateExistingHistory(
            () => UpperLimitManagementStatementPolicy.ValidateHistory(history, historyLines));
        var head = history.MaxBy(item => item.Revision);
        ClaimInputSaveGuard.ValidateRequestedRevision(
            request.Kind, request.ExpectedHeadId, head?.Id);

        var id = Guid.NewGuid();
        var entity = new UpperLimitManagementStatement
        {
            Id = id,
            RootId = head?.RootId ?? id,
            Revision = UpperLimitManagementStatementPolicy.NextRevision(history, historyLines),
            Kind = request.Kind,
            ExpectedHeadId = request.ExpectedHeadId,
            ServiceMonth = request.ServiceMonth,
            RecipientId = request.RecipientId,
            CertificateId = request.CertificateId,
            ManagingOfficeId = request.ManagingOfficeId,
            MunicipalityNumber = request.MunicipalityNumber,
            CertificateNumber = request.CertificateNumber,
            CertificateMonthlyCostCap = request.CertificateMonthlyCostCap,
            UpperLimitManagementApplicability = request.UpperLimitManagementApplicability,
            CertificateManagingOfficeNumber = request.CertificateManagingOfficeNumber,
            ManagingOfficeNumber = request.ManagingOfficeNumber,
            ManagingOfficeName = request.ManagingOfficeName,
            OriginalCreationKind = request.OriginalCreationKind,
            ReceivedAt = request.ReceivedAt,
            OriginalDocumentReference = request.OriginalDocumentReference,
            IsConfirmed = request.IsConfirmed,
            ConfirmedAt = request.ConfirmedAt,
            ConfirmedBy = request.ConfirmedBy,
            ConfirmationReason = request.ConfirmationReason,
            Result = request.Result,
            TotalCostYen = request.TotalCostYen,
            TotalPreManagementBurdenYen = request.TotalPreManagementBurdenYen,
            TotalManagedBurdenYen = request.TotalManagedBurdenYen,
            CreatedAt = clock.GetUtcNow(),
            CreatedBy = actor,
            ConcurrencyToken = Guid.NewGuid(),
        };
        var now = entity.CreatedAt;
        var lines = request.Lines.Select(line => new UpperLimitManagementStatementLine
        {
            Id = Guid.NewGuid(),
            StatementId = entity.Id,
            LineNumber = line.LineNumber,
            OfficeNumber = line.OfficeNumber,
            OfficeName = line.OfficeName,
            TotalCostYen = line.TotalCostYen,
            PreManagementBurdenYen = line.PreManagementBurdenYen,
            ManagedBurdenYen = line.ManagedBurdenYen,
            CreatedAt = now,
            CreatedBy = actor,
            ConcurrencyToken = Guid.NewGuid(),
        }).ToArray();

        ClaimInputSaveGuard.ValidateCandidate(() =>
            UpperLimitManagementStatementPolicy.ValidateHistory(
                [.. history, entity], [.. historyLines, .. lines]));
        await repo.AddAsync(entity, lines, ct);
        await uow.SaveChangesAsync(ct);
        return ClaimInputRevisionMapper.Map(entity, entity.RootId, entity.Revision,
            entity.Kind, entity.ExpectedHeadId);
    }
}

internal static class ClaimInputRevisionMapper
{
    public static ClaimInputRevisionDto Map(
        Entity entity,
        Guid rootId,
        int revision,
        Domain.Enums.RecordKind kind,
        Guid? expectedHeadId) =>
        new(
            entity.Id,
            rootId,
            revision,
            kind,
            expectedHeadId,
            entity.CreatedAt,
            entity.CreatedBy);
}
