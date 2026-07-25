namespace Tsumugi.Application.Claim;

/// <summary>
/// readiness 要件（<see cref="ClaimInputRequirement"/>）の条件評価と存在判定。
/// </summary>
/// <remarks>
/// <para>
/// 2 つの経路から使う。(1) 確定前の readiness（<see cref="ClaimPreparationReadiness"/>。現行の DB 状態から
/// 作った値を評価する）。(2) 確定済み請求を別の仕様版の要件で調べる経路
/// （<see cref="ClaimFinalizationReadinessContextBuilder"/> が確定 snapshot から作った値を評価する）。
/// </para>
/// <para>
/// 条件評価を 2 か所に持つと、版が動いたときに「確定時は通ったのに出力時は落ちる（またはその逆）」が
/// 検証できなくなるため、ここに 1 つだけ置く。
/// </para>
/// </remarks>
internal static class ClaimRequirementEvaluator
{
    internal static void AddMissingRequirementIssue(
        ClaimInputRequirement requirement,
        Guid? recipientId,
        IReadOnlyDictionary<string, ClaimPreparationValue> values,
        IReadOnlySet<string> rowScopes,
        HashSet<ClaimPreparationIssue> issues)
    {
        var condition = EvaluateCondition(requirement.Condition, values, rowScopes);
        if (condition == ConditionEvaluation.NotApplicable
            || (condition == ConditionEvaluation.Applies
                && IsPresent(requirement.TargetPath, values)))
        {
            return;
        }

        issues.Add(new ClaimPreparationIssue(
            condition == ConditionEvaluation.Unresolved
                ? ClaimPreparationIssueCode.UnresolvedRequirementCondition
                : ClaimPreparationIssueCode.MissingRequiredField,
            recipientId,
            requirement.TargetPath,
            requirement.Destination));
    }

    private static ConditionEvaluation EvaluateCondition(
        ClaimRequirementCondition condition,
        IReadOnlyDictionary<string, ClaimPreparationValue> values,
        IReadOnlySet<string> rowScopes) =>
        condition switch
        {
            ClaimRequirementCondition.Always => ConditionEvaluation.Applies,
            ClaimRequirementCondition.ModelPresent present =>
                EvaluatePresent(present.ModelPath, values),
            ClaimRequirementCondition.ModelNonZero nonZero =>
                EvaluateNonZero(nonZero.ModelPath, values),
            ClaimRequirementCondition.ModelTrue modelTrue =>
                EvaluateTrue(modelTrue.ModelPath, values),
            ClaimRequirementCondition.RowPresent rowPresent =>
                rowScopes.Contains(rowPresent.RowScope)
                    ? ConditionEvaluation.Applies
                    : ConditionEvaluation.NotApplicable,
            ClaimRequirementCondition.ModelIn modelIn =>
                EvaluateIn(modelIn, values),
            ClaimRequirementCondition.All all =>
                EvaluateAll(all, values, rowScopes),
            ClaimRequirementCondition.Any any =>
                EvaluateAny(any, values, rowScopes),
            _ => throw new InvalidOperationException("Unsupported claim requirement condition."),
        };

    private static ConditionEvaluation EvaluatePresent(
        string path,
        IReadOnlyDictionary<string, ClaimPreparationValue> values)
    {
        if (!values.TryGetValue(path, out var value))
        {
            return ConditionEvaluation.Unresolved;
        }

        return value.Kind == ClaimPreparationValueKind.NotApplicable
            ? ConditionEvaluation.NotApplicable
            : ConditionEvaluation.Applies;
    }

    private static ConditionEvaluation EvaluateNonZero(
        string path,
        IReadOnlyDictionary<string, ClaimPreparationValue> values)
    {
        if (!values.TryGetValue(path, out var value))
        {
            return ConditionEvaluation.Unresolved;
        }

        if (value.Kind == ClaimPreparationValueKind.NotApplicable)
        {
            return ConditionEvaluation.NotApplicable;
        }

        if (value.Kind != ClaimPreparationValueKind.Number)
        {
            return ConditionEvaluation.Unresolved;
        }

        return value.NumberValue != 0
            ? ConditionEvaluation.Applies
            : ConditionEvaluation.NotApplicable;
    }

    private static ConditionEvaluation EvaluateTrue(
        string path,
        IReadOnlyDictionary<string, ClaimPreparationValue> values)
    {
        if (!values.TryGetValue(path, out var value))
        {
            return ConditionEvaluation.Unresolved;
        }

        if (value.Kind == ClaimPreparationValueKind.NotApplicable)
        {
            return ConditionEvaluation.NotApplicable;
        }

        if (value.Kind != ClaimPreparationValueKind.Boolean)
        {
            return ConditionEvaluation.Unresolved;
        }

        return value.BooleanValue == true
            ? ConditionEvaluation.Applies
            : ConditionEvaluation.NotApplicable;
    }

    private static ConditionEvaluation EvaluateIn(
        ClaimRequirementCondition.ModelIn condition,
        IReadOnlyDictionary<string, ClaimPreparationValue> values)
    {
        if (!values.TryGetValue(condition.ModelPath, out var value))
        {
            return ConditionEvaluation.Unresolved;
        }

        if (value.Kind == ClaimPreparationValueKind.NotApplicable)
        {
            return ConditionEvaluation.NotApplicable;
        }

        if ((value.Kind != ClaimPreparationValueKind.Code
                && value.Kind != ClaimPreparationValueKind.Text)
            || value.StringValue is null)
        {
            return ConditionEvaluation.Unresolved;
        }

        return condition.AllowedValues.Contains(value.StringValue, StringComparer.Ordinal)
            ? ConditionEvaluation.Applies
            : ConditionEvaluation.NotApplicable;
    }

    private static ConditionEvaluation EvaluateAll(
        ClaimRequirementCondition.All condition,
        IReadOnlyDictionary<string, ClaimPreparationValue> values,
        IReadOnlySet<string> rowScopes)
    {
        var evaluations = condition.Conditions
            .Select(child => EvaluateCondition(child, values, rowScopes))
            .ToArray();
        if (evaluations.Contains(ConditionEvaluation.NotApplicable))
        {
            return ConditionEvaluation.NotApplicable;
        }

        return evaluations.Contains(ConditionEvaluation.Unresolved)
            ? ConditionEvaluation.Unresolved
            : ConditionEvaluation.Applies;
    }

    private static ConditionEvaluation EvaluateAny(
        ClaimRequirementCondition.Any condition,
        IReadOnlyDictionary<string, ClaimPreparationValue> values,
        IReadOnlySet<string> rowScopes)
    {
        var evaluations = condition.Conditions
            .Select(child => EvaluateCondition(child, values, rowScopes))
            .ToArray();
        if (evaluations.Contains(ConditionEvaluation.Applies))
        {
            return ConditionEvaluation.Applies;
        }

        return evaluations.Contains(ConditionEvaluation.Unresolved)
            ? ConditionEvaluation.Unresolved
            : ConditionEvaluation.NotApplicable;
    }

    private static bool IsPresent(
        string path,
        IReadOnlyDictionary<string, ClaimPreparationValue> values) =>
        values.TryGetValue(path, out var value)
        && value.Kind is not ClaimPreparationValueKind.Unknown
            and not ClaimPreparationValueKind.NotApplicable;

    private enum ConditionEvaluation
    {
        NotApplicable = 0,
        Applies = 1,
        Unresolved = 2,
    }
}
