using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;

namespace Tsumugi.Application.UseCases.Claim;

public sealed class SetClaimInputUseCase(
    IClaimInputRepository repo,
    IUnitOfWork uow,
    IClaimGenericFieldCatalog genericFieldCatalog,
    IClaimCsvSpecificationVersions specificationVersions,
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
        var genericValues = BuildGenericValues(request, id);
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
            SpecialVisitSupportBilledCount = request.SpecialVisitSupportBilledCount,
            OffsiteSupportCumulativeDays = request.OffsiteSupportCumulativeDays,
            GenericValues = genericValues,
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

    /// <summary>
    /// 汎用 pass-through 入力（ADR 0042）を検証して行へ変換する。宣言（名前・型・桁数）は CSV 仕様側が
    /// 正本で、ここでは <see cref="IClaimGenericFieldCatalog"/> から取った宣言だけを信じる
    /// （Application に fieldId や桁数をハードコードしない）。未宣言の名前は保存しない。
    /// </summary>
    private ClaimInputGenericValue[] BuildGenericValues(SetClaimInputRequest request, Guid claimInputId)
    {
        var entered = request.GenericValues
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToArray();
        if (entered.Length == 0) return [];

        if (request.Kind == RecordKind.Cancel)
        {
            throw new ClaimInputSaveException(
                ClaimInputSaveErrorCode.InvalidValue,
                ClaimInputFieldCode.Values,
                "取消は請求入力値を持てません。値を消してから取消してください。");
        }

        var version = specificationVersions.Current;
        return [.. entered
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair =>
            {
                var value = pair.Value!.Trim();
                // 宣言の有無・型・桁数の判定は仕様を所有する層に委ねる（未宣言の名前もここで弾かれる）。
                // 検証理由は利用者向けなので、履歴競合（再読込）ではなく値のエラーとして返す。
                try
                {
                    genericFieldCatalog.ValidateValue(version, pair.Key, value);
                }
                catch (ClaimGenericValueInvalidException invalid)
                {
                    throw new ClaimInputSaveException(
                        ClaimInputSaveErrorCode.InvalidValue,
                        ClaimInputFieldCode.Values,
                        invalid.Message);
                }

                return ClaimInputGenericValue.Create(Guid.NewGuid(), claimInputId, pair.Key, value);
            })];
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
            || request.StandardUsageDayTotal is < 0
            // 制度上の限度（訪問支援特別加算の月内算定回数上限・施設外支援の累計日数上限）は
            // 公式実値をコードに持ち込まないため検証しない（CLAUDE.md §ハード制約3）。負値だけを弾く。
            || request.SpecialVisitSupportBilledCount is < 0
            || request.OffsiteSupportCumulativeDays is < 0)
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
