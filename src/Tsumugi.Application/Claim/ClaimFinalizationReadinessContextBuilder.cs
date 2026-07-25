using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Claim;

/// <summary>
/// 確定 snapshot（<see cref="ClaimFinalizationSnapshot"/>）から readiness の値を組み、
/// 指定した仕様版の要件で「項目が入っているか」を判定する。
/// </summary>
/// <remarks>
/// <para>
/// 答えるのは <b>「その版が要求する項目が確定 snapshot に入っているか」だけ</b>。
/// 受給者証の確認記録（evidence）・上限額管理結果票・証の重複といった<b>確定時点の検査は再現しない</b>
/// （確定 snapshot がそれらを運ばないため）。再現できないものを既定値で埋めると、偽の合格または
/// 偽の不足を出す。したがって本クラスが返す issue は
/// <see cref="ClaimPreparationIssueCode.MissingRequiredField"/> と
/// <see cref="ClaimPreparationIssueCode.UnresolvedRequirementCondition"/> に限られる（ADR 0041）。
/// </para>
/// <para>
/// 値の組み立ては <see cref="ClaimPreparationContextBuilder"/> と同じ関数を通す。path キーを 2 か所に
/// 書くと、片方にパスを足して他方を忘れるドリフトが起きる。
/// </para>
/// </remarks>
public static class ClaimFinalizationReadinessContextBuilder
{
    /// <summary>
    /// 確定 snapshot が、指定した版の要件を満たしているかを調べる。返る一覧が空なら満たしている。
    /// </summary>
    public static IReadOnlyList<ClaimPreparationIssue> Evaluate(
        ClaimFinalizationSnapshot snapshot,
        IReadOnlyList<ClaimInputRequirement> requirements,
        IReadOnlyCollection<string>? declaredGenericNames = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(requirements);

        var values = BuildValues(snapshot, declaredGenericNames);
        var rowScopes = BuildRowScopes(snapshot);
        var issues = new HashSet<ClaimPreparationIssue>();
        foreach (var requirement in requirements)
        {
            ClaimRequirementEvaluator.AddMissingRequirementIssue(
                requirement, snapshot.RecipientId, values, rowScopes, issues);
        }

        return [.. issues];
    }

    /// <summary>確定 snapshot 由来の readiness 値（事業所＋受給者を 1 つの辞書にまとめる）。</summary>
    internal static IReadOnlyDictionary<string, ClaimPreparationValue> BuildValues(
        ClaimFinalizationSnapshot snapshot,
        IReadOnlyCollection<string>? declaredGenericNames = null)
    {
        var values = ClaimPreparationContextBuilder.BuildOfficeValues(new ClaimReadinessOffice(
            snapshot.Office.PostalCode,
            snapshot.Office.Address,
            snapshot.Office.PhoneNumber,
            snapshot.Office.RepresentativeTitleAndName));

        foreach (var pair in ClaimPreparationContextBuilder.BuildRecipientValues(
            ClaimInputOf(snapshot.ClaimInput),
            new ClaimReadinessCertificate(
                snapshot.Certificate.MunicipalityNumber,
                snapshot.Certificate.SubsidyMunicipalityNumber,
                snapshot.Certificate.UpperLimitManagementProviderNumber),
            ContractedProviderOf(snapshot.ContractedProvider),
            AggregateOf(snapshot.DailyRecords),
            snapshot.IntensiveSupportEpisode?.StartDate,
            declaredGenericNames))
        {
            values[pair.Key] = pair.Value;
        }

        return values;
    }

    private static ClaimReadinessClaimInput ClaimInputOf(ClaimFinalizationClaimInputSnapshot input) => new(
        ParseEnum<UpperLimitManagementResult>(input.UpperLimitManagementResult),
        input.UpperLimitManagedAmountYen,
        input.MunicipalSubsidyAmountYen,
        input.ExceptionalUsageStartMonth,
        input.ExceptionalUsageEndMonth,
        input.ExceptionalUsageDays,
        input.StandardUsageDayTotal,
        input.SpecialVisitSupportBilledCount,
        input.OffsiteSupportCumulativeDays,
        input.GenericValues.ToDictionary(
            value => value.Name, value => value.Value, StringComparer.Ordinal));

    private static ClaimReadinessContractedProvider ContractedProviderOf(
        ClaimFinalizationContractedProviderSnapshot? contractedProvider)
        => contractedProvider is null
            ? ClaimReadinessContractedProvider.Absent
            : new ClaimReadinessContractedProvider(
                contractedProvider.CertificateEntryNumber, contractedProvider.FirstServiceDate);

    /// <summary>
    /// 日次記録の縮約。<see cref="ClaimDailyRecordAggregate"/> の doc-comment と同じ規則で、
    /// 本体報酬を算定する日（<see cref="Attendance.Present"/>）だけを母集団にする。
    /// </summary>
    private static ClaimDailyRecordAggregate AggregateOf(
        IReadOnlyList<ClaimFinalizationDailyRecordSnapshot> dailyRecords)
    {
        var presentDays = dailyRecords
            .Where(record => record.Attendance == Attendance.Present)
            .OrderBy(record => record.ServiceDate)
            .ToArray();
        if (presentDays.Length == 0) return ClaimDailyRecordAggregate.Empty;

        return new ClaimDailyRecordAggregate(
            ServiceStartTime: presentDays.Select(record => record.ServiceStartTime).FirstOrDefault(),
            ServiceEndTime: presentDays.Select(record => record.ServiceEndTime).FirstOrDefault(),
            SpecialVisitSupportMinutesTotal:
                presentDays.Sum(record => record.SpecialVisitSupportMinutes ?? 0),
            OffsiteSupportApplied: presentDays.Any(record => record.OffsiteSupportApplied),
            MedicalCoordinationType: FirstEnum<MedicalCoordinationType>(
                presentDays.Select(record => record.MedicalCoordinationType)),
            TrialUseSupportType: FirstEnum<TrialUseSupportType>(
                presentDays.Select(record => record.TrialUseSupportType)),
            RegionalCollaborationApplied: presentDays.Any(record => record.RegionalCollaborationApplied),
            IntensiveSupportApplied: presentDays.Any(record => record.IntensiveSupportApplied),
            EmergencyAdmissionApplied: presentDays.Any(record => record.EmergencyAdmissionApplied),
            RecipientConfirmation: presentDays.All(record => record.RecipientConfirmation)
                ? RecipientConfirmationStatus.Confirmed
                : RecipientConfirmationStatus.Unspecified,
            // 未入力（どの日にも値が無い）は null。0 を渡すと「入力済みの 0」と区別できず fail-open する。
            SpecialVisitSupportBilledHoursTotal:
                presentDays.Any(record => record.SpecialVisitSupportBilledHours is not null)
                    ? presentDays.Sum(record => record.SpecialVisitSupportBilledHours ?? 0)
                    : null);
    }

    /// <summary>
    /// 行スコープ。実績記録票の日次行は本体報酬算定日が 1 日以上あれば存在し、
    /// 集中支援行は当月いずれかの算定日で集中支援を算定していれば存在する
    /// （<see cref="ClaimPreparationContextBuilder"/> と同じ規則）。
    /// </summary>
    private static HashSet<string> BuildRowScopes(ClaimFinalizationSnapshot snapshot)
    {
        var scopes = new HashSet<string>(StringComparer.Ordinal);
        if (snapshot.BilledDays > 0)
        {
            scopes.Add(ClaimPreparationContextBuilder.DailyRecordRowScope);
        }

        if (AggregateOf(snapshot.DailyRecords).IntensiveSupportApplied)
        {
            scopes.Add(ClaimPreparationContextBuilder.IntensiveSupportRowScope);
        }

        return scopes;
    }

    private static TEnum FirstEnum<TEnum>(IEnumerable<string?> tokens)
        where TEnum : struct, Enum
        => tokens.Select(ParseEnum<TEnum>).FirstOrDefault(value => value is not null) ?? default;

    private static TEnum? ParseEnum<TEnum>(string? token)
        where TEnum : struct, Enum
        => token is not null && Enum.TryParse<TEnum>(token, ignoreCase: false, out var parsed)
            ? parsed
            : null;
}
