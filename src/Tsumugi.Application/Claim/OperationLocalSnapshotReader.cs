using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos.Claim.Reports;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Claim;

/// <summary>
/// <see cref="IOperationLocalSnapshotReader"/>のproduction実装。Office/Recipient/Certificateは
/// 単一実体、DailyRecordは月次履歴、IntensiveSupportEpisode/ClaimInputは追記型revision chainを
/// それぞれのrepositoryから並列取得し（<see cref="Task.WhenAll(Task[])"/>）、各chainはDomainの
/// 対応Policyで実効値へ縮約する（履歴不正はPolicy側の例外がそのまま伝播しフェイルクローズする）。
/// 対象実体が存在しない場合は<see cref="InvalidOperationException"/>で拒否する（確定操作を
/// readiness未成立のまま進めない防御。readiness gate本体は別contextの責務）。
/// </summary>
public sealed class OperationLocalSnapshotReader(
    IOfficeRepository officeRepository,
    IRecipientRepository recipientRepository,
    ICertificateRepository certificateRepository,
    IDailyRecordRepository dailyRecordRepository,
    IIntensiveSupportEpisodeRepository intensiveSupportEpisodeRepository,
    IClaimInputRepository claimInputRepository) : IOperationLocalSnapshotReader
{
    public async Task<ClaimFinalizationSnapshot> ReadAsync(
        Guid officeId,
        Guid recipientId,
        ServiceMonth serviceMonth,
        RecipientClaimResult calculationResult,
        string claimMasterVersion,
        string csvSpecificationVersion,
        string reportSpecificationVersion,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(calculationResult);
        ArgumentException.ThrowIfNullOrWhiteSpace(claimMasterVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(csvSpecificationVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportSpecificationVersion);

        var monthStart = new DateOnly(serviceMonth.Year, serviceMonth.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var officeTask = officeRepository.FindByIdAsync(officeId, ct);
        var recipientTask = recipientRepository.FindByIdAsync(recipientId, ct);
        var certificateTask = certificateRepository.FindEffectiveAsync(recipientId, monthEnd, ct);
        var dailyRecordsTask = dailyRecordRepository.ListByRecipientAndMonthAsync(
            recipientId, serviceMonth.Year, serviceMonth.Month, ct);
        var intensiveSupportHistoryTask = intensiveSupportEpisodeRepository.ListHistoryAsync(
            officeId, recipientId, ct);
        var claimInputHistoryTask = claimInputRepository.ListHistoryAsync(
            officeId, recipientId, serviceMonth, ct);

        await Task.WhenAll(
            officeTask,
            recipientTask,
            certificateTask,
            dailyRecordsTask,
            intensiveSupportHistoryTask,
            claimInputHistoryTask);

        var office = officeTask.Result
            ?? throw new InvalidOperationException($"Office id={officeId} が存在しない。");
        var recipient = recipientTask.Result
            ?? throw new InvalidOperationException($"Recipient id={recipientId} が存在しない。");
        var certificate = certificateTask.Result
            ?? throw new InvalidOperationException(
                $"recipientId={recipientId} のserviceMonth={serviceMonth}時点で実効なCertificateが存在しない。");

        var claimInput = ClaimInputPolicy.Effective(claimInputHistoryTask.Result);
        var intensiveSupportEpisode = IntensiveSupportEpisodePolicy.Effective(intensiveSupportHistoryTask.Result);
        var dailyRecordSnapshots = DailyRecordPolicy.EffectiveByDate(dailyRecordsTask.Result)
            .Values
            .OrderBy(record => record.ServiceDate)
            .Select(BuildDailyRecordSnapshot)
            .ToArray();

        return new ClaimFinalizationSnapshot(
            recipientId,
            serviceMonth,
            claimMasterVersion,
            csvSpecificationVersion,
            reportSpecificationVersion,
            BuildOfficeSnapshot(office),
            new ClaimFinalizationRecipientSnapshot(recipient.KanjiName, recipient.KanaName),
            BuildCertificateSnapshot(certificate),
            BuildClaimInputSnapshot(claimInput),
            dailyRecordSnapshots,
            intensiveSupportEpisode is { StartDate: { } startDate }
                ? new ClaimFinalizationIntensiveSupportEpisodeSnapshot(startDate)
                : null,
            BuildClaimLines(calculationResult),
            calculationResult.BilledDays,
            calculationResult.TotalUnits,
            calculationResult.TotalCostYen,
            calculationResult.BenefitYen,
            calculationResult.BurdenYen);
    }

    private static ClaimFinalizationOfficeSnapshot BuildOfficeSnapshot(Office office)
        => new(
            office.OfficeNumber,
            office.Name,
            office.RegionGrade,
            office.PostalCode
                ?? throw new InvalidOperationException($"Office id={office.Id} のPostalCodeが未入力。"),
            office.Address
                ?? throw new InvalidOperationException($"Office id={office.Id} のAddressが未入力。"),
            office.PhoneNumber
                ?? throw new InvalidOperationException($"Office id={office.Id} のPhoneNumberが未入力。"),
            office.RepresentativeTitleAndName
                ?? throw new InvalidOperationException(
                    $"Office id={office.Id} のRepresentativeTitleAndNameが未入力。"));

    private static ClaimFinalizationCertificateSnapshot BuildCertificateSnapshot(Certificate certificate)
        => new(
            certificate.CertificateNumber,
            certificate.MunicipalityNumber
                ?? throw new InvalidOperationException(
                    $"Certificate id={certificate.Id} のMunicipalityNumberが未入力。"),
            certificate.SubsidyMunicipalityNumber,
            certificate.MonthlyCostCap,
            certificate.UpperLimitManagementProviderNumber,
            certificate.UpperLimitManagementProvider);

    private static ClaimFinalizationClaimInputSnapshot BuildClaimInputSnapshot(ClaimInput? claimInput)
        => new(
            claimInput?.UpperLimitManagementResult?.ToString(),
            claimInput?.UpperLimitManagedAmountYen,
            claimInput?.MunicipalSubsidyAmountYen,
            claimInput?.ExceptionalUsageStartMonth,
            claimInput?.ExceptionalUsageEndMonth,
            claimInput?.ExceptionalUsageDays,
            claimInput?.StandardUsageDayTotal);

    private static ClaimFinalizationDailyRecordSnapshot BuildDailyRecordSnapshot(DailyRecord record)
        => new(
            record.ServiceDate,
            record.Attendance,
            record.MealProvided,
            record.Transport,
            record.Note,
            record.ServiceStartTime,
            record.ServiceEndTime,
            record.SpecialVisitSupportMinutes,
            record.OffsiteSupportApplied ?? false,
            record.MedicalCoordinationType == MedicalCoordinationType.Unspecified
                ? null
                : record.MedicalCoordinationType.ToString(),
            record.TrialUseSupportType == TrialUseSupportType.Unspecified
                ? null
                : record.TrialUseSupportType.ToString(),
            record.RegionalCollaborationApplied ?? false,
            record.IntensiveSupportApplied ?? false,
            record.EmergencyAdmissionApplied ?? false,
            record.RecipientConfirmation == RecipientConfirmationStatus.Confirmed);

    /// <summary>
    /// <see cref="RecipientClaimResult"/>から基本報酬1行＋加算明細行群を構築する。基本報酬の
    /// <c>Unit</c>（1回あたり単位数）は<c>(TotalUnits - Σ加算Units) / BilledDays</c>で復元する
    /// （<c>ClaimCalculator.BuildDetail</c>のbaseUnits = UnitsPerDay(int) × BilledDaysという
    /// 構成上、常に整除できる）。<c>AmountYen</c>は地域単価（decimal）がこのreaderへ渡されず
    /// 再算定もしない設計上、行単位の金額を一意に復元できない。そのため
    /// <c>TotalCostYen / TotalUnits</c>の近似単価で加算行の金額を概算し、残余を基本報酬行へ
    /// 寄せることで「Σ(行のAmountYen) = TotalCostYen」の整合性のみを保証する（真の地域単価に
    /// よる行内訳の再現ではない、表示用の近似値）。<c>docs/phase3-claim-field-mapping.md</c>の
    /// 明細書claim-lines行（provider:J121:03:xxx）は単位数・回数・サービス単位数（単位数×回数）
    /// のみを要求し金額列を持たないため、現行の21 report fieldsの契約には影響しない
    /// （Task 10-12で実際の描画要件を確認し、必要なら本近似の要否を再検討する）。
    /// </summary>
    private static List<ClaimFinalizationClaimLineSnapshot> BuildClaimLines(
        RecipientClaimResult result)
    {
        var additionUnitsSum = result.AdditionLines.Sum(line => line.Units);
        var basicUnits = result.TotalUnits - additionUnitsSum;
        var basicUnitPerOccurrence = result.BilledDays > 0 ? basicUnits / result.BilledDays : basicUnits;
        var unitPriceApprox = result.TotalUnits > 0
            ? (decimal)result.TotalCostYen / result.TotalUnits
            : 0m;

        var additionSnapshots = new List<ClaimFinalizationClaimLineSnapshot>(result.AdditionLines.Count);
        var additionsAmountYenSum = 0;
        foreach (var addition in result.AdditionLines)
        {
            var amountYen = (int)Math.Floor(addition.Units * unitPriceApprox);
            additionsAmountYenSum += amountYen;
            additionSnapshots.Add(new ClaimFinalizationClaimLineSnapshot(
                ClaimDetailLineKind.Addition, addition.ServiceCode, addition.Units, 1, amountYen));
        }

        var lines = new List<ClaimFinalizationClaimLineSnapshot>(1 + additionSnapshots.Count)
        {
            new(
                ClaimDetailLineKind.Basic,
                result.ServiceCode,
                basicUnitPerOccurrence,
                result.BilledDays,
                result.TotalCostYen - additionsAmountYenSum),
        };
        lines.AddRange(additionSnapshots);
        return lines;
    }
}
