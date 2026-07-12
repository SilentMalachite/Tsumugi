using System.Collections.ObjectModel;

namespace Tsumugi.Application.Claim;

public enum ClaimInputDestination
{
    Unknown = 0,
    Certificate = 1,
    ClaimInput = 2,
    ClaimPreparation = 3,
    DailyRecord = 4,
    Office = 5,
}

public abstract record ClaimRequirementCondition
{
    internal ClaimRequirementCondition()
    {
    }

    public sealed record Always : ClaimRequirementCondition;

    public sealed record ModelPresent(string ModelPath) : ClaimRequirementCondition;

    public sealed record ModelNonZero(string ModelPath) : ClaimRequirementCondition;

    public sealed record ModelTrue(string ModelPath) : ClaimRequirementCondition;

    public sealed record RowPresent(string RowScope) : ClaimRequirementCondition;

    public sealed record ModelIn : ClaimRequirementCondition
    {
        public ModelIn(string modelPath, IEnumerable<string> allowedValues)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
            ArgumentNullException.ThrowIfNull(allowedValues);
            var copiedValues = allowedValues.ToArray();
            if (copiedValues.Length == 0
                || copiedValues.Any(string.IsNullOrWhiteSpace)
                || copiedValues.Distinct(StringComparer.Ordinal).Count() != copiedValues.Length)
                throw new ArgumentException("Allowed values must be non-empty and unique.", nameof(allowedValues));

            ModelPath = modelPath;
            AllowedValues = Array.AsReadOnly(copiedValues);
        }

        public string ModelPath { get; }
        public ReadOnlyCollection<string> AllowedValues { get; }
    }

    public sealed record All : ClaimRequirementCondition
    {
        public All(IEnumerable<ClaimRequirementCondition> conditions)
        {
            Conditions = CopyConditions(conditions);
        }

        public ReadOnlyCollection<ClaimRequirementCondition> Conditions { get; }
    }

    public sealed record Any : ClaimRequirementCondition
    {
        public Any(IEnumerable<ClaimRequirementCondition> conditions)
        {
            Conditions = CopyConditions(conditions);
        }

        public ReadOnlyCollection<ClaimRequirementCondition> Conditions { get; }
    }

    private static ReadOnlyCollection<ClaimRequirementCondition> CopyConditions(
        IEnumerable<ClaimRequirementCondition> conditions)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        var copiedConditions = conditions.ToArray();
        if (copiedConditions.Length == 0 || copiedConditions.Any(condition => condition is null))
            throw new ArgumentException("Conditions must not be empty.", nameof(conditions));
        return Array.AsReadOnly(copiedConditions);
    }
}

public sealed record ClaimInputRequirement
{
    public ClaimInputRequirement(
        string targetPath,
        IEnumerable<string> fieldIds,
        ClaimRequirementCondition condition,
        ClaimInputDestination destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(fieldIds);
        ArgumentNullException.ThrowIfNull(condition);
        var copiedFieldIds = fieldIds.ToArray();
        if (copiedFieldIds.Length == 0
            || copiedFieldIds.Any(string.IsNullOrWhiteSpace)
            || copiedFieldIds.Distinct(StringComparer.Ordinal).Count() != copiedFieldIds.Length)
            throw new ArgumentException("Field IDs must be non-empty and unique.", nameof(fieldIds));
        if (!Enum.IsDefined(destination) || destination == ClaimInputDestination.Unknown)
            throw new ArgumentOutOfRangeException(nameof(destination));

        TargetPath = targetPath;
        FieldIds = Array.AsReadOnly(copiedFieldIds);
        Condition = condition;
        Destination = destination;
    }

    public string TargetPath { get; }
    public ReadOnlyCollection<string> FieldIds { get; }
    public ClaimRequirementCondition Condition { get; }
    public ClaimInputDestination Destination { get; }
}
