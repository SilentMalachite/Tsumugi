using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic.Claim.Models;

public readonly record struct EnteredYen
{
    public EnteredYen(bool isEntered, int? valueYen)
    {
        if (!isEntered && valueYen is not null)
            throw new ArgumentException("未入力の金額に値を設定できません。", nameof(valueYen));
        if (isEntered && valueYen is null or < 0)
            throw new ArgumentException("入力済み金額には非負の値が必要です。", nameof(valueYen));

        IsEntered = isEntered;
        ValueYen = valueYen;
    }

    public bool IsEntered { get; }
    public int? ValueYen { get; }
}

public enum Article31SpecialBurdenStatus
{
    Unknown = 0,
    NotApplicable = 1,
    Applicable = 2,
}

public enum FiscalYearCompleteness
{
    Unknown = 0,
    Incomplete = 1,
    Complete = 2,
}

public enum AverageWageBandOptionKind
{
    Unknown = 0,
    Numeric = 1,
    FiledTransition = 2,
    ProductionActivitySupport = 3,
}

public readonly record struct AverageWageBandOption
{
    public AverageWageBandOption(AverageWageBandOptionKind kind, int officialOptionCode)
    {
        if (!Enum.IsDefined(kind) || kind == AverageWageBandOptionKind.Unknown)
            throw new ArgumentException("平均工賃区分optionの種別が不正です。", nameof(kind));
        if (officialOptionCode <= 0)
            throw new ArgumentException("公式option codeは正の整数でなければなりません。", nameof(officialOptionCode));

        Kind = kind;
        OfficialOptionCode = officialOptionCode;
    }

    public AverageWageBandOptionKind Kind { get; }
    public int OfficialOptionCode { get; }
}

public readonly record struct VersionedAverageWageBandOption
{
    public VersionedAverageWageBandOption(
        ClaimMasterVersion masterVersion,
        AverageWageBandOption option)
        : this(masterVersion, option.Kind, option.OfficialOptionCode)
    {
    }

    internal VersionedAverageWageBandOption(
        ClaimMasterVersion masterVersion,
        AverageWageBandOptionKind kind,
        int officialOptionCode)
    {
        _ = masterVersion.Value;
        if (!Enum.IsDefined(kind) || kind == AverageWageBandOptionKind.Unknown)
            throw new ArgumentException("版付き平均工賃optionの種別が不正です。", nameof(kind));
        if (officialOptionCode <= 0)
            throw new ArgumentException("版付き平均工賃option codeは正の整数でなければなりません。", nameof(officialOptionCode));

        MasterVersion = masterVersion;
        Kind = kind;
        OfficialOptionCode = officialOptionCode;
    }

    public ClaimMasterVersion MasterVersion { get; }
    internal AverageWageBandOptionKind Kind { get; }
    internal int OfficialOptionCode { get; }
    public AverageWageBandOption Option => new(Kind, OfficialOptionCode);
}

public sealed class AverageWageBandOptionVersionRule
{
    private readonly HashSet<AverageWageBandOption> _allowedOptions;
    private readonly Dictionary<R8ReformStatus, HashSet<AverageWageBandOption>> _allowedOptionsByStatus;

    public AverageWageBandOptionVersionRule(
        ClaimMasterVersion masterVersion,
        ServiceMonth effectiveFrom,
        ServiceMonth? effectiveTo,
        IEnumerable<AverageWageBandOption> allowedOptions,
        IReadOnlyDictionary<R8ReformStatus, IReadOnlyCollection<AverageWageBandOption>> allowedOptionsByStatus)
    {
        ArgumentNullException.ThrowIfNull(allowedOptions);
        ArgumentNullException.ThrowIfNull(allowedOptionsByStatus);
        _ = masterVersion.Value;
        _ = effectiveFrom.ToInt();
        if (effectiveTo is { } end && end < effectiveFrom)
            throw new ArgumentException("平均工賃option版の有効期間が逆転しています。", nameof(effectiveTo));

        var materializedOptions = allowedOptions.ToArray();
        _allowedOptions = materializedOptions.ToHashSet();
        if (_allowedOptions.Count == 0 || _allowedOptions.Count != materializedOptions.Length
            || _allowedOptions.Any(option => option.Kind == AverageWageBandOptionKind.Unknown
                                              || option.OfficialOptionCode <= 0))
            throw new ArgumentException("平均工賃option版の許可集合が空、不正又は重複しています。", nameof(allowedOptions));

        _allowedOptionsByStatus = [];
        foreach (var (status, options) in allowedOptionsByStatus)
        {
            if (!Enum.IsDefined(status) || status == R8ReformStatus.Unknown)
                throw new ArgumentException("平均工賃option版のR8状態が不正です。", nameof(allowedOptionsByStatus));
            var materialized = options.ToArray();
            var set = materialized.ToHashSet();
            if (set.Count == 0 || set.Count != materialized.Length || set.Any(option => !_allowedOptions.Contains(option)))
                throw new ArgumentException("R8状態別option集合が空、不正又は重複しています。", nameof(allowedOptionsByStatus));
            _allowedOptionsByStatus.Add(status, set);
        }

        MasterVersion = masterVersion;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
    }

    public ClaimMasterVersion MasterVersion { get; }
    public ServiceMonth EffectiveFrom { get; }
    public ServiceMonth? EffectiveTo { get; }

    public bool AppliesTo(ServiceMonth month) =>
        month >= EffectiveFrom && (EffectiveTo is null || month <= EffectiveTo.Value);

    public bool Allows(AverageWageBandOption option) => _allowedOptions.Contains(option);

    public bool Allows(R8ReformStatus status, AverageWageBandOption option) =>
        _allowedOptionsByStatus.TryGetValue(status, out var options) && options.Contains(option);
}

public enum R8ReformStatus
{
    Unknown = 0,
    NotApplicableBeforeR8 = 1,
    ReformTarget = 2,
    ReformExempt = 3,
    UnchangedBelow15000 = 4,
}

public enum UpperLimitManagementApplicability
{
    Unknown = 0,
    NotApplicable = 1,
    Applicable = 2,
}

/// <summary>公式の上限額管理結果区分。</summary>
public enum UpperLimitManagementResult
{
    Result1 = 1,
    Result2 = 2,
    Result3 = 3,
}
