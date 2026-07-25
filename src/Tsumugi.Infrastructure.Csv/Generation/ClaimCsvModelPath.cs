using System.Globalization;
using Tsumugi.Application.Dtos.Claim.Csv;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Infrastructure.Csv.Generation;

/// <summary>
/// <c>field-mapping-r7-10.json</c> の <c>modelPath</c> / <c>targetModel.targetProperty</c> /
/// generatorRule の <c>selector</c> が指すモデル経路を、<see cref="ClaimCsvDto"/> 上で解決する。
/// </summary>
/// <remarks>
/// finalization snapshot v2 に含まれない経路（<c>ContractedProvider.*</c> /
/// <c>DailyRecord.Note</c> / <c>ClaimCalculation.InitialAdditionStartDate</c> /
/// <c>ClaimCalculation.SpecialMeasuresYen</c> / <c>ClaimServiceLine.SummaryNote</c>）は
/// <see cref="ClaimCsvValue.Missing"/> を返す。これらはいずれも requiredWhen が
/// <c>modelPresent</c> 系または歴史的条件のため、空欄が正しい出力になる。
/// 常時必須の経路が欠けた場合は呼び出し側（encoder）が MissingRequired で fail-close する。
/// </remarks>
internal static class ClaimCsvModelPath
{
    /// <summary>modelIn / modelEquals のトークンを数値へ解く際に使う列挙型の対応表。</summary>
    private static readonly Dictionary<string, Type> EnumTypeByPath = new(StringComparer.Ordinal)
    {
        ["DailyRecord.Transport"] = typeof(TransportKind),
        ["DailyRecord.Attendance"] = typeof(Attendance),
        ["DailyRecord.MedicalCoordinationType"] = typeof(MedicalCoordinationType),
        ["DailyRecord.TrialUseSupportType"] = typeof(TrialUseSupportType),
    };

    /// <summary>snapshot v2 に含まれないことが判明している経路（空欄が正しい出力になる）。</summary>
    internal static IReadOnlySet<string> PathsAbsentFromSnapshot { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "ContractedProvider.ContractedSupplyDays",
            "ContractedProvider.ContractDate",
            "ContractedProvider.TerminationDate",
            "ContractedProvider.CertificateEntryNumber",
            "DailyRecord.Note",
            "ClaimCalculation.InitialAdditionStartDate",
            "ClaimCalculation.SpecialMeasuresYen",
            "ClaimServiceLine.SummaryNote",
        };

    internal static bool TryResolveEnumToken(string path, string token, out long value)
    {
        value = 0;
        if (!EnumTypeByPath.TryGetValue(path, out var enumType)) return false;
        // 列挙型メンバー名の解析。CultureInfo: 非該当（書式変換を伴わない）
        if (!Enum.TryParse(enumType, token, ignoreCase: false, out var parsed) || parsed is null) // CultureInfo: 非該当
        {
            return false;
        }

        value = Convert.ToInt64(parsed, CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>受給者単位の値だが、ファイル全体で単一であることが請求の前提になる経路の接頭辞。</summary>
    private static readonly string[] RecipientUniformPrefixes =
        ["Certificate.", "Recipient.", "ClaimInput.", "IntensiveSupportEpisode."];

    internal static ClaimCsvValue Resolve(string path, ClaimCsvResolutionScope scope)
    {
        if (PathsAbsentFromSnapshot.Contains(path)) return ClaimCsvValue.Missing;

        // 請求書（provider:J111:*）はファイル単位のレコードだが、市町村番号のように
        // 受給者側に持つ値を書く項目がある。国保連請求は市町村単位の束であることが前提のため、
        // 全受給者で値が一致することを要求し、割れていたら fail-close する。
        if (scope.Recipient is null
            && RecipientUniformPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return ResolveUniformAcrossRecipients(path, scope);
        }

        return path switch
        {
            "Office.OfficeNumber" => ClaimCsvValue.FromText(scope.Dto.Office.OfficeNumber),

            "Recipient.KanaName" => ClaimCsvValue.FromText(scope.RequireRecipient(path).RecipientKanaName),

            "Certificate.CertificateNumber" =>
                ClaimCsvValue.FromText(scope.RequireRecipient(path).CertificateNumber),
            "Certificate.MunicipalityNumber" =>
                ClaimCsvValue.FromText(scope.RequireRecipient(path).MunicipalityNumber),
            "Certificate.SubsidyMunicipalityNumber" =>
                ClaimCsvValue.FromText(scope.RequireRecipient(path).SubsidyMunicipalityNumber),
            "Certificate.MonthlyCostCap" =>
                ClaimCsvValue.FromNumber(scope.RequireRecipient(path).MonthlyCostCapYen),
            "Certificate.UpperLimitManagementProviderNumber" =>
                ClaimCsvValue.FromText(scope.RequireRecipient(path).UpperLimitManagementProviderNumber),

            "ClaimInput.UpperLimitManagementResult" => ClaimCsvValue.FromOptionalNumber(
                scope.RequireRecipient(path).UpperLimitManagementResultCode),
            "ClaimInput.UpperLimitManagedAmountYen" => ClaimCsvValue.FromOptionalNumber(
                scope.RequireRecipient(path).UpperLimitManagedAmountYen),
            "ClaimInput.MunicipalSubsidyAmountYen" => ClaimCsvValue.FromOptionalNumber(
                scope.RequireRecipient(path).MunicipalSubsidyAmountYen),
            "ClaimInput.ExceptionalUsageStartMonth" => ClaimCsvValue.FromOptional(
                scope.RequireRecipient(path).ExceptionalUsageStartMonth, ClaimCsvValue.FromMonth),
            "ClaimInput.ExceptionalUsageEndMonth" => ClaimCsvValue.FromOptional(
                scope.RequireRecipient(path).ExceptionalUsageEndMonth, ClaimCsvValue.FromMonth),
            "ClaimInput.ExceptionalUsageDays" => ClaimCsvValue.FromOptionalNumber(
                scope.RequireRecipient(path).ExceptionalUsageDays),
            "ClaimInput.StandardUsageDayTotal" => ClaimCsvValue.FromOptionalNumber(
                scope.RequireRecipient(path).StandardUsageDayTotal),

            "IntensiveSupportEpisode.StartDate" => ClaimCsvValue.FromOptional(
                scope.RequireRecipient(path).IntensiveSupportEpisodeStartDate, ClaimCsvValue.FromDate),

            "DailyRecord.ServiceDate" => ClaimCsvValue.FromDate(scope.RequireDay(path).ServiceDate),
            "DailyRecord.Attendance" => ClaimCsvValue.FromNumber(scope.RequireDay(path).AttendanceCode),
            "DailyRecord.Transport" => ClaimCsvValue.FromNumber(scope.RequireDay(path).TransportCode),
            "DailyRecord.MealProvided" => Flag(scope.RequireDay(path).MealProvided),
            "DailyRecord.ServiceStartTime" => ClaimCsvValue.FromOptional(
                scope.RequireDay(path).ServiceStartTime, ClaimCsvValue.FromTime),
            "DailyRecord.ServiceEndTime" => ClaimCsvValue.FromOptional(
                scope.RequireDay(path).ServiceEndTime, ClaimCsvValue.FromTime),
            "DailyRecord.SpecialVisitSupportMinutes" => ClaimCsvValue.FromOptionalNumber(
                scope.RequireDay(path).SpecialVisitSupportMinutes),
            "DailyRecord.OffsiteSupportApplied" => Flag(scope.RequireDay(path).OffsiteSupportApplied),
            "DailyRecord.MedicalCoordinationType" => ClaimCsvValue.FromOptionalNumber(
                scope.RequireDay(path).MedicalCoordinationCode),
            "DailyRecord.TrialUseSupportType" => ClaimCsvValue.FromOptionalNumber(
                scope.RequireDay(path).TrialUseSupportCode),
            "DailyRecord.RegionalCollaborationApplied" =>
                Flag(scope.RequireDay(path).RegionalCollaborationApplied),
            "DailyRecord.IntensiveSupportApplied" => Flag(scope.RequireDay(path).IntensiveSupportApplied),
            "DailyRecord.EmergencyAdmissionApplied" => Flag(scope.RequireDay(path).EmergencyAdmissionApplied),

            "ClaimServiceLine.ServiceCode" => ClaimCsvValue.FromText(scope.RequireLine(path).ServiceCode),
            "ClaimServiceLine.UnitCount" => ClaimCsvValue.FromNumber(scope.RequireLine(path).Unit),
            "ClaimServiceLine.Count" => ClaimCsvValue.FromNumber(scope.RequireLine(path).Count),

            "RegionalClassificationMaster.byOfficeAndServiceProvisionMonth" =>
                ClaimCsvValue.FromText(scope.Dto.Office.RegionClassificationCode),
            "UnitPriceMaster.byRegionServiceTypeAndServiceProvisionMonth" =>
                ClaimCsvValue.FromNumber(scope.Dto.Office.UnitPriceMilliYen),

            _ => throw new ClaimCsvGenerationException(
                scope.FieldId,
                ClaimCsvGenerationReason.UnresolvableModelPath,
                $"model path '{path}' is not known to the CSV generator"),
        };
    }

    private static ClaimCsvValue ResolveUniformAcrossRecipients(
        string path,
        ClaimCsvResolutionScope scope)
    {
        if (scope.Dto.Recipients.Count == 0)
        {
            throw new ClaimCsvGenerationException(
                scope.FieldId,
                ClaimCsvGenerationReason.MissingRow,
                $"model path '{path}' requires at least one recipient");
        }

        var values = scope.Dto.Recipients
            .Select((_, index) => Resolve(path, scope with
            {
                Row = scope.Row with { RecipientIndex = index },
            }))
            .Distinct()
            .ToArray();

        return values.Length == 1
            ? values[0]
            : throw new ClaimCsvGenerationException(
                scope.FieldId,
                ClaimCsvGenerationReason.UnresolvableModelPath,
                $"model path '{path}' differs across recipients; a claim file must cover a single value");
    }

    /// <summary>真偽フラグは「該当する/しない」だけを表し、値そのものは持たない。</summary>
    private static ClaimCsvValue Flag(bool value) =>
        value ? ClaimCsvValue.FromNumber(1) : ClaimCsvValue.Missing;
}
