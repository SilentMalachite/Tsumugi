using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;

namespace Tsumugi.Application.UseCases.Claim;

public sealed class SetClaimInputUseCase(
    IClaimInputRepository repo,
    IUnitOfWork uow,
    TimeProvider clock)
{
    public async Task<ClaimInputRevisionDto> ExecuteAsync(
        SetClaimInputRequest request,
        string actor,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ClaimInputSaveGuard.ValidateActor(actor);
        ClaimInputSaveGuard.ValidateIdentities(request.OfficeId, request.RecipientId);

        var history = await repo.ListHistoryAsync(
            request.OfficeId, request.RecipientId, request.ServiceMonth, ct);
        ClaimInputSaveGuard.ValidateExistingHistory(
            () => ClaimInputPolicy.ValidateHistory(history));

        var head = history.MaxBy(item => item.Revision);
        ClaimInputSaveGuard.ValidateRequestedRevision(
            request.Kind, request.ExpectedHeadId, head?.Id);
        ValidateCrossFieldValues(request);

        var id = Guid.NewGuid();
        var entity = new ClaimInput
        {
            Id = id,
            OfficeId = request.OfficeId,
            RecipientId = request.RecipientId,
            ServiceMonth = request.ServiceMonth,
            RootId = head?.RootId ?? id,
            Revision = ClaimInputPolicy.NextRevision(history),
            Kind = request.Kind,
            ExpectedHeadId = request.ExpectedHeadId,
            UpperLimitManagementResult = request.UpperLimitManagementResult,
            UpperLimitManagedAmountYen = request.UpperLimitManagedAmountYen,
            MunicipalSubsidyAmountYen = request.MunicipalSubsidyAmountYen,
            ExceptionalUsageStartMonth = request.ExceptionalUsageStartMonth,
            ExceptionalUsageEndMonth = request.ExceptionalUsageEndMonth,
            ExceptionalUsageDays = request.ExceptionalUsageDays,
            StandardUsageDayTotal = request.StandardUsageDayTotal,
            CreatedAt = clock.GetUtcNow(),
            CreatedBy = actor,
            ConcurrencyToken = Guid.NewGuid(),
        };

        ClaimInputSaveGuard.ValidateCandidate(
            () => ClaimInputPolicy.ValidateHistory([.. history, entity]));
        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return Map(entity);
    }

    private static void ValidateCrossFieldValues(SetClaimInputRequest request)
    {
        if (request.Kind == RecordKind.Cancel) return;

        var hasResult = request.UpperLimitManagementResult is not null;
        var hasManagedAmount = request.UpperLimitManagedAmountYen is not null;
        var hasExceptionalStart = request.ExceptionalUsageStartMonth is not null;
        var hasExceptionalEnd = request.ExceptionalUsageEndMonth is not null;
        if (hasResult != hasManagedAmount
            || hasExceptionalStart != hasExceptionalEnd
            || request.ExceptionalUsageStartMonth > request.ExceptionalUsageEndMonth
            || request.UpperLimitManagedAmountYen is < 0
            || request.MunicipalSubsidyAmountYen is < 0
            || request.ExceptionalUsageDays is < 0
            || request.StandardUsageDayTotal is < 0)
            throw new ClaimInputSaveException(
                ClaimInputSaveErrorCode.InvalidValue,
                ClaimInputFieldCode.Values);
    }

    private static ClaimInputRevisionDto Map(ClaimInput entity) =>
        new(
            entity.Id,
            entity.RootId,
            entity.Revision,
            entity.Kind,
            entity.ExpectedHeadId,
            entity.CreatedAt,
            entity.CreatedBy);
}

public sealed class SetIntensiveSupportEpisodeUseCase(
    IIntensiveSupportEpisodeRepository repo,
    IUnitOfWork uow,
    TimeProvider clock)
{
    public async Task<ClaimInputRevisionDto> ExecuteAsync(
        SetIntensiveSupportEpisodeRequest request,
        string actor,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ClaimInputSaveGuard.ValidateActor(actor);
        ClaimInputSaveGuard.ValidateIdentities(request.OfficeId, request.RecipientId);

        var history = await repo.ListHistoryAsync(
            request.OfficeId, request.RecipientId, ct);
        ClaimInputSaveGuard.ValidateExistingHistory(
            () => IntensiveSupportEpisodePolicy.ValidateHistory(history));
        var head = history.MaxBy(item => item.Revision);
        ClaimInputSaveGuard.ValidateRequestedRevision(
            request.Kind, request.ExpectedHeadId, head?.Id);

        var id = Guid.NewGuid();
        var entity = new IntensiveSupportEpisode
        {
            Id = id,
            OfficeId = request.OfficeId,
            RecipientId = request.RecipientId,
            RootId = head?.RootId ?? id,
            Revision = IntensiveSupportEpisodePolicy.NextRevision(history),
            Kind = request.Kind,
            ExpectedHeadId = request.ExpectedHeadId,
            StartDate = request.StartDate,
            CreatedAt = clock.GetUtcNow(),
            CreatedBy = actor,
            ConcurrencyToken = Guid.NewGuid(),
        };

        ClaimInputSaveGuard.ValidateCandidate(
            () => IntensiveSupportEpisodePolicy.ValidateHistory([.. history, entity]));
        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return new ClaimInputRevisionDto(
            entity.Id,
            entity.RootId,
            entity.Revision,
            entity.Kind,
            entity.ExpectedHeadId,
            entity.CreatedAt,
            entity.CreatedBy);
    }
}

internal static class ClaimInputSaveGuard
{
    public static void ValidateActor(string actor)
    {
        if (string.IsNullOrWhiteSpace(actor))
            throw new ClaimInputSaveException(
                ClaimInputSaveErrorCode.InvalidRequest,
                ClaimInputFieldCode.Actor);
    }

    public static void ValidateIdentities(params Guid[] values)
    {
        if (values.Any(value => value == Guid.Empty))
            throw new ClaimInputSaveException(
                ClaimInputSaveErrorCode.InvalidRequest,
                ClaimInputFieldCode.Identity);
    }

    public static void ValidateRequestedRevision(
        RecordKind kind,
        Guid? expectedHeadId,
        Guid? currentHeadId)
    {
        if (kind is not RecordKind.New and not RecordKind.Correct and not RecordKind.Cancel)
            throw new ClaimInputSaveException(
                ClaimInputSaveErrorCode.InvalidRequest,
                ClaimInputFieldCode.RecordKind);

        if (currentHeadId is null)
        {
            if (kind != RecordKind.New)
                throw new ClaimInputSaveException(
                    ClaimInputSaveErrorCode.ExpectedHeadRequired,
                    ClaimInputFieldCode.ExpectedHead);
            if (expectedHeadId is not null)
                throw new ClaimInputSaveException(
                    ClaimInputSaveErrorCode.ExpectedHeadMismatch,
                    ClaimInputFieldCode.ExpectedHead);
            return;
        }

        if (expectedHeadId is null || expectedHeadId == Guid.Empty)
            throw new ClaimInputSaveException(
                ClaimInputSaveErrorCode.ExpectedHeadRequired,
                ClaimInputFieldCode.ExpectedHead);
        if (kind == RecordKind.New || expectedHeadId != currentHeadId)
            throw new ClaimInputSaveException(
                ClaimInputSaveErrorCode.ExpectedHeadMismatch,
                ClaimInputFieldCode.ExpectedHead);
    }

    public static void ValidateExistingHistory(Action validate) =>
        ValidatePolicy(validate, ClaimInputSaveErrorCode.InvalidHistory,
            ClaimInputFieldCode.History);

    public static void ValidateCandidate(Action validate) =>
        ValidatePolicy(validate, ClaimInputSaveErrorCode.InvalidValue,
            ClaimInputFieldCode.Values);

    private static void ValidatePolicy(
        Action validate,
        ClaimInputSaveErrorCode code,
        ClaimInputFieldCode fieldCode)
    {
        try
        {
            validate();
        }
        catch (InvalidOperationException)
        {
            throw new ClaimInputSaveException(code, fieldCode);
        }
        catch (ArgumentException)
        {
            throw new ClaimInputSaveException(code, fieldCode);
        }
    }
}
