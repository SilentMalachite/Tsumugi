using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic.Claim;

/// <summary>事業所請求profileの追記型履歴と版付き登録を検証する。</summary>
public sealed class OfficeClaimProfilePolicy
{
    private readonly ClaimMasterVersion _masterVersion;
    private readonly AverageWageBandOptionVersionRule[] _optionRules;
    private readonly DateOnly _r8EffectiveDate;
    private readonly Func<DateOnly, DateOnly> _filedTransitionExclusiveEndResolver;

    public OfficeClaimProfilePolicy(
        ClaimMasterVersion masterVersion,
        IReadOnlyCollection<AverageWageBandOptionVersionRule> optionRules,
        DateOnly r8EffectiveDate,
        Func<DateOnly, DateOnly> filedTransitionExclusiveEndResolver)
    {
        ArgumentNullException.ThrowIfNull(optionRules);
        ArgumentNullException.ThrowIfNull(filedTransitionExclusiveEndResolver);
        _ = masterVersion.Value;
        if (r8EffectiveDate == DateOnly.MinValue)
            throw new ArgumentException("R8施行日が未初期化です。", nameof(r8EffectiveDate));
        _masterVersion = masterVersion;
        _optionRules = optionRules.ToArray();
        _r8EffectiveDate = r8EffectiveDate;
        _filedTransitionExclusiveEndResolver = filedTransitionExclusiveEndResolver;
        if (_optionRules.Length == 0 || !_optionRules.Any(rule => rule.MasterVersion == masterVersion))
            throw new ArgumentException("対象master版の平均工賃option ruleがありません。", nameof(optionRules));
        for (var left = 0; left < _optionRules.Length; left++)
            for (var right = left + 1; right < _optionRules.Length; right++)
            {
                if (_optionRules[left].MasterVersion == _optionRules[right].MasterVersion
                    && PeriodsOverlap(_optionRules[left], _optionRules[right]))
                    throw new ArgumentException("同じmaster版の平均工賃option ruleが重複しています。", nameof(optionRules));
            }
    }

    public void ValidateHistory(IReadOnlyCollection<OfficeClaimProfile> history)
    {
        ArgumentNullException.ThrowIfNull(history);
        if (history.Count == 0) return;

        var ordered = history.OrderBy(item => item.Revision).ToArray();
        var ids = new HashSet<Guid>();
        for (var index = 0; index < ordered.Length; index++)
        {
            var item = ordered[index];
            if (!ids.Add(item.Id)) throw Invalid("OfficeClaimProfile履歴内でIDが重複しています。");
            if (item.Revision != index + 1) throw Invalid("OfficeClaimProfileのRevisionが連続していません。");
        }

        var root = ordered[0];
        if (root.Id == Guid.Empty || root.RootId != root.Id) throw Invalid("Revision 1はroot自身でなければなりません。");
        if (root.Kind != RecordKind.New || root.ExpectedHeadId is not null) throw Invalid("Revision 1の履歴metadataが不正です。");
        if (root.OfficeId == Guid.Empty || root.EffectiveFrom == DateOnly.MinValue
            || root.EffectiveTo < root.EffectiveFrom)
            throw Invalid("事業所又は有効期間が不正です。");

        for (var index = 0; index < ordered.Length; index++)
        {
            var item = ordered[index];
            if (item.Id == Guid.Empty || item.RootId != root.Id) throw Invalid("root IDが履歴内で一致していません。");
            if (item.OfficeId != root.OfficeId || item.EffectiveFrom != root.EffectiveFrom || item.EffectiveTo != root.EffectiveTo)
                throw Invalid("profileの事業所又は有効期間が履歴内で一致していません。");
            if (item.Kind is not RecordKind.New and not RecordKind.Correct and not RecordKind.Cancel)
                throw Invalid("未知のRecordKindです。");
            if (index > 0)
            {
                if (item.Kind == RecordKind.New) throw Invalid("Revision 1以外にNewを追加できません。");
                if (item.ExpectedHeadId is null || item.ExpectedHeadId == Guid.Empty || item.ExpectedHeadId != ordered[index - 1].Id)
                    throw Invalid("expected headは直前Revisionを指す必要があります。");
            }

            if (item.Kind == RecordKind.Cancel) ValidateCancellation(item);
            else ValidateValue(item);
        }
    }

    public OfficeClaimProfile? Effective(IReadOnlyCollection<OfficeClaimProfile> history)
    {
        ValidateHistory(history);
        var head = history.Count == 0 ? null : history.MaxBy(item => item.Revision);
        return head?.Kind == RecordKind.Cancel ? null : head;
    }

    public int NextRevision(IReadOnlyCollection<OfficeClaimProfile> history)
    {
        ValidateHistory(history);
        return history.Count == 0 ? 1 : checked(history.Max(item => item.Revision) + 1);
    }

    private void ValidateValue(OfficeClaimProfile item)
    {
        if (item.EffectiveTo < item.EffectiveFrom) throw Invalid("有効期間が逆転しています。");
        if (item.MasterVersion is not { } masterVersion || masterVersion != _masterVersion)
            throw Invalid("請求master版と許可option集合の版が一致しません。");
        if (item.ReformStatus is null or R8ReformStatus.Unknown || !Enum.IsDefined(item.ReformStatus.Value))
            throw Invalid("R8改定状態が不明です。");
        ValidateReformPeriod(item);
        if (item.AverageWageBandOption is not { } option)
            throw Invalid("版で許可されていない平均工賃optionです。");
        var currentRule = ResolveCurrentRule(item, masterVersion);
        if (!currentRule.Allows(item.ReformStatus.Value, option))
            throw Invalid("R8改定状態と平均工賃optionの組合せが版ruleと一致しません。");
        if (string.IsNullOrWhiteSpace(item.EvidenceDocumentId)
            || item.ConfirmedAt is null || item.ConfirmedAt == DateTimeOffset.MinValue
            || string.IsNullOrWhiteSpace(item.ConfirmedBy) || string.IsNullOrWhiteSpace(item.ConfirmationReason))
            throw Invalid("指定権者確認証跡が不足しています。");

        ValidateReformComparison(item);

        if (option.Kind == AverageWageBandOptionKind.FiledTransition)
        {
            if (item.DesignationDate is not { } designation || item.SupportStartDate is not { } supportStart
                || item.EffectiveTo is not { } effectiveTo || item.EffectiveFrom < designation
                || supportStart < designation || supportStart > effectiveTo
                || !TryResolveTransitionExclusiveEnd(designation, out var transitionExclusiveEnd)
                || effectiveTo >= transitionExclusiveEnd
                || item.FiledTransitionPeriod is not { } transitionPeriod
                || transitionPeriod.Start != item.EffectiveFrom || transitionPeriod.End != effectiveTo
                || string.IsNullOrWhiteSpace(item.FiledTransitionEvidenceDocumentId))
                throw Invalid("FiledTransitionの公式初期期間と指定履歴が矛盾しています。");
        }
        else if (item.FiledTransitionPeriod is not null
                 || !string.IsNullOrWhiteSpace(item.FiledTransitionEvidenceDocumentId))
        {
            throw Invalid("数値optionにFiledTransition根拠を設定できません。");
        }

        ValidateBillingTokenFields(item);
    }

    /// <summary>
    /// 定員(実頭数)・人員配置区分token・地域区分tokenは存在するときだけ形式を検証する
    /// （Task 9b）。語彙自体（マスタ側の閉集合）はここでは検証しない。
    /// </summary>
    private static void ValidateBillingTokenFields(OfficeClaimProfile item)
    {
        if (item.CapacityHeadcount is { } capacityHeadcount && capacityHeadcount < 1)
            throw Invalid("利用定員(実頭数)は1以上である必要があります。");
        if (item.StaffingKey is not null && string.IsNullOrWhiteSpace(item.StaffingKey))
            throw Invalid("人員配置区分tokenが空白です。");
        if (item.RegionKey is not null && string.IsNullOrWhiteSpace(item.RegionKey))
            throw Invalid("地域区分tokenが空白です。");
    }

    private void ValidateReformPeriod(OfficeClaimProfile item)
    {
        var endsBeforeR8 = item.EffectiveTo is { } effectiveTo && effectiveTo < _r8EffectiveDate;
        var startsAtOrAfterR8 = item.EffectiveFrom >= _r8EffectiveDate;
        if (!endsBeforeR8 && !startsAtOrAfterR8)
            throw Invalid("R8施行月を跨ぐprofileは期間を分割する必要があります。");
        if (endsBeforeR8 && item.ReformStatus != R8ReformStatus.NotApplicableBeforeR8)
            throw Invalid("R8施行前のprofileには施行前状態が必要です。");
        if (startsAtOrAfterR8 && item.ReformStatus == R8ReformStatus.NotApplicableBeforeR8)
            throw Invalid("R8施行後に施行前状態を使用できません。");
    }

    private void ValidateReformComparison(OfficeClaimProfile item)
    {
        var comparisonProvided = item.EarlierRegisteredBandOption is not null
                                 || item.EarlierRegistrationMonth is not null
                                 || item.LaterRegisteredBandOption is not null
                                 || item.LaterRegistrationMonth is not null
                                 || !string.IsNullOrWhiteSpace(item.ReformComparisonEvidenceDocumentId);
        if (item.ReformStatus != R8ReformStatus.ReformExempt && !comparisonProvided) return;

        if (item.EarlierRegisteredBandOption is not { } earlierOption
            || item.EarlierRegistrationMonth is not { } earlierMonth
            || item.LaterRegisteredBandOption is not { } laterOption
            || item.LaterRegistrationMonth is not { } laterMonth
            || string.IsNullOrWhiteSpace(item.ReformComparisonEvidenceDocumentId)
            || earlierOption.Option.Kind != AverageWageBandOptionKind.Numeric
            || laterOption.Option.Kind != AverageWageBandOptionKind.Numeric
            || earlierMonth >= laterMonth
            || new DateOnly(laterMonth.Year, laterMonth.Month, 1) >= _r8EffectiveDate)
            throw Invalid("R8改定対象外判定の公式登録区分比較が不足または矛盾しています。");

        ValidateRegisteredOption(earlierOption, earlierMonth);
        ValidateRegisteredOption(laterOption, laterMonth);
    }

    private AverageWageBandOptionVersionRule ResolveCurrentRule(
        OfficeClaimProfile item,
        ClaimMasterVersion masterVersion)
    {
        var from = new ServiceMonth(item.EffectiveFrom.Year, item.EffectiveFrom.Month);
        ServiceMonth? to = item.EffectiveTo is { } effectiveTo
            ? new ServiceMonth(effectiveTo.Year, effectiveTo.Month)
            : null;
        var candidates = _optionRules.Where(rule =>
            rule.MasterVersion == masterVersion
            && rule.AppliesTo(from)
            && (to is { } end ? rule.AppliesTo(end) : rule.EffectiveTo is null)).ToArray();
        if (candidates.Length != 1)
            throw Invalid("profile期間に適用できる平均工賃option版を一意に解決できません。");
        return candidates[0];
    }

    private void ValidateRegisteredOption(
        VersionedAverageWageBandOption registeredOption,
        ServiceMonth registrationMonth)
    {
        var candidates = _optionRules.Where(rule =>
            rule.MasterVersion == registeredOption.MasterVersion
            && rule.AppliesTo(registrationMonth)
            && rule.Allows(registeredOption.Option)).ToArray();
        if (candidates.Length != 1)
            throw Invalid("過去比較optionの版、登録月又は公式optionが一致しません。");
    }

    private static bool PeriodsOverlap(
        AverageWageBandOptionVersionRule left,
        AverageWageBandOptionVersionRule right)
    {
        var leftEnd = left.EffectiveTo ?? new ServiceMonth(2200, 12);
        var rightEnd = right.EffectiveTo ?? new ServiceMonth(2200, 12);
        return left.EffectiveFrom <= rightEnd && right.EffectiveFrom <= leftEnd;
    }

    private bool TryResolveTransitionExclusiveEnd(DateOnly designation, out DateOnly exclusiveEnd)
    {
        try
        {
            exclusiveEnd = _filedTransitionExclusiveEndResolver(designation);
            return exclusiveEnd > designation;
        }
        catch (ArgumentOutOfRangeException)
        {
            exclusiveEnd = default;
            return false;
        }
    }

    private static void ValidateCancellation(OfficeClaimProfile item)
    {
        if (item.MasterVersion is not null || item.ReformStatus is not null
            || item.AverageWageBandOption is not null || item.DesignationDate is not null
            || item.SupportStartDate is not null || item.EarlierRegisteredBandOption is not null
            || item.EarlierRegistrationMonth is not null || item.LaterRegisteredBandOption is not null
            || item.LaterRegistrationMonth is not null || item.ReformComparisonEvidenceDocumentId is not null
            || item.FiledTransitionPeriod is not null || item.FiledTransitionEvidenceDocumentId is not null
            || item.EvidenceDocumentId is not null || item.ConfirmedAt is not null
            || item.ConfirmedBy is not null || item.ConfirmationReason is not null
            || item.CapacityHeadcount is not null || item.StaffingKey is not null
            || item.RegionKey is not null)
            throw Invalid("取消には事業所請求profileの業務値を保持できません。");
    }

    private static InvalidOperationException Invalid(string message) => new(message);
}
