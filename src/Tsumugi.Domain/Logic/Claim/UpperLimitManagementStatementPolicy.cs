using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;

namespace Tsumugi.Domain.Logic.Claim;

/// <summary>正式な上限額管理結果票の追記型履歴と全事業所行を検証する。</summary>
public static class UpperLimitManagementStatementPolicy
{
    public static void ValidateHistory(
        IReadOnlyCollection<UpperLimitManagementStatement> history,
        IReadOnlyCollection<UpperLimitManagementStatementLine> lines)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(lines);
        if (history.Count == 0)
        {
            if (lines.Count != 0) throw Invalid("上限額管理結果票に属さない行があります。");
            return;
        }

        var ordered = history.OrderBy(statement => statement.Revision).ToArray();
        var ids = new HashSet<Guid>();
        for (var index = 0; index < ordered.Length; index++)
        {
            if (!ids.Add(ordered[index].Id))
                throw Invalid("上限額管理結果票履歴内でIDが重複しています。");
            if (ordered[index].Revision != index + 1)
                throw Invalid("上限額管理結果票のRevisionが欠落または重複しています。");
        }

        var root = ordered[0];
        if (root.Id == Guid.Empty || root.RootId == Guid.Empty || root.Id != root.RootId)
            throw Invalid("上限額管理結果票Revision 1はroot自身でなければなりません。");
        if (root.Kind != RecordKind.New || root.ExpectedHeadId is not null)
            throw Invalid("上限額管理結果票Revision 1はNewでexpected headを持てません。");
        if (root.RecipientId == Guid.Empty || root.CertificateId == Guid.Empty || root.ManagingOfficeId == Guid.Empty)
            throw Invalid("上限額管理結果票のbusiness keyが不正です。");
        _ = root.ServiceMonth.ToInt();

        var statementIds = ordered.Select(statement => statement.Id).ToHashSet();
        var lineIds = new HashSet<Guid>();
        foreach (var line in lines)
        {
            if (line.Id == Guid.Empty || !lineIds.Add(line.Id))
                throw Invalid("上限額管理結果票の行IDが空または重複しています。");
            if (!statementIds.Contains(line.StatementId))
                throw Invalid("上限額管理結果票に属さない行があります。");
        }

        for (var index = 0; index < ordered.Length; index++)
        {
            var statement = ordered[index];
            if (statement.Id == Guid.Empty || statement.RootId != root.Id)
                throw Invalid("上限額管理結果票のroot IDが一致しません。");
            if (statement.ServiceMonth != root.ServiceMonth
                || statement.RecipientId != root.RecipientId
                || statement.CertificateId != root.CertificateId
                || statement.ManagingOfficeId != root.ManagingOfficeId)
                throw Invalid("上限額管理結果票履歴に異なる月、利用者、証または事業所が混在しています。");
            if (statement.Kind is not RecordKind.New and not RecordKind.Correct and not RecordKind.Cancel)
                throw Invalid("上限額管理結果票のRecordKindが不正です。");

            if (index > 0)
            {
                if (statement.Kind == RecordKind.New)
                    throw Invalid("上限額管理結果票Revision 1以外にNewを追加できません。");
                if (statement.ExpectedHeadId is null
                    || statement.ExpectedHeadId == Guid.Empty
                    || statement.ExpectedHeadId != ordered[index - 1].Id)
                    throw Invalid("上限額管理結果票のexpected headが直前Revisionと一致しません。");
            }

            var statementLines = lines.Where(line => line.StatementId == statement.Id).ToArray();
            if (statement.Kind == RecordKind.Cancel)
            {
                if (statementLines.Length != 0)
                    throw Invalid("取消された上限額管理結果票に行を追加できません。");
                ValidateCancellation(statement);
            }
            else
            {
                ValidateStatement(statement, statementLines);
            }
        }
    }

    public static UpperLimitManagementStatement? Effective(
        IReadOnlyCollection<UpperLimitManagementStatement> history,
        IReadOnlyCollection<UpperLimitManagementStatementLine> lines)
    {
        ValidateHistory(history, lines);
        if (history.Count == 0) return null;
        var head = history.MaxBy(statement => statement.Revision)!;
        return head.Kind == RecordKind.Cancel ? null : head;
    }

    public static int NextRevision(
        IReadOnlyCollection<UpperLimitManagementStatement> history,
        IReadOnlyCollection<UpperLimitManagementStatementLine> lines)
    {
        ValidateHistory(history, lines);
        return history.Count == 0 ? 1 : checked(history.Max(statement => statement.Revision) + 1);
    }

    private static void ValidateStatement(
        UpperLimitManagementStatement statement,
        UpperLimitManagementStatementLine[] lines)
    {
        if (!statement.IsConfirmed
            || statement.ReceivedAt is null || statement.ReceivedAt == DateTimeOffset.MinValue
            || statement.ConfirmedAt is null || statement.ConfirmedAt == DateTimeOffset.MinValue
            || string.IsNullOrWhiteSpace(statement.ConfirmedBy)
            || string.IsNullOrWhiteSpace(statement.ConfirmationReason)
            || string.IsNullOrWhiteSpace(statement.OriginalDocumentReference))
            throw Invalid("上限額管理結果票は原本確認済みでなければなりません。");
        if (string.IsNullOrWhiteSpace(statement.MunicipalityNumber)
            || string.IsNullOrWhiteSpace(statement.CertificateNumber)
            || string.IsNullOrWhiteSpace(statement.ManagingOfficeNumber)
            || string.IsNullOrWhiteSpace(statement.ManagingOfficeName)
            || string.IsNullOrWhiteSpace(statement.OriginalCreationKind))
            throw Invalid("上限額管理結果票の必須識別情報が欠落しています。");
        if (!statement.CertificateMonthlyCostCap.IsEntered
            || statement.CertificateMonthlyCostCap.ValueYen is not { } certificateCap)
            throw Invalid("上限額管理結果票には入力済みの証上限が必要です。");
        if (statement.UpperLimitManagementApplicability != UpperLimitManagementApplicability.Applicable
            || !Enum.IsDefined(statement.UpperLimitManagementApplicability))
            throw Invalid("上限額管理結果票の対象状態が不正です。");
        if (string.IsNullOrWhiteSpace(statement.CertificateManagingOfficeNumber)
            || !string.Equals(statement.CertificateManagingOfficeNumber, statement.ManagingOfficeNumber,
                StringComparison.Ordinal))
            throw Invalid("証と結果票の管理事業所番号が一致しません。");
        if (!Enum.IsDefined(statement.Result))
            throw Invalid("上限額管理結果票の結果区分が不正です。");
        var headerTotalCost = RequireEntered(statement.TotalCostYen);
        var headerTotalPre = RequireEntered(statement.TotalPreManagementBurdenYen);
        var headerTotalManaged = RequireEntered(statement.TotalManagedBurdenYen);
        if (lines.Length == 0)
            throw Invalid("上限額管理結果票には1行以上必要です。");

        var lineNumbers = new HashSet<int>();
        var officeNumbers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            if (line.LineNumber <= 0 || !lineNumbers.Add(line.LineNumber))
                throw Invalid("上限額管理結果票の行番号が不正または重複しています。");
            if (string.IsNullOrWhiteSpace(line.OfficeNumber)
                || string.IsNullOrWhiteSpace(line.OfficeName)
                || !officeNumbers.Add(line.OfficeNumber))
                throw Invalid("上限額管理結果票の事業所行が不正または重複しています。");
            var linePre = RequireEntered(line.PreManagementBurdenYen);
            var lineManaged = RequireEntered(line.ManagedBurdenYen);
            _ = RequireEntered(line.TotalCostYen);
            if (lineManaged > linePre)
                throw Invalid("管理結果後額は管理前額を超えられません。");
        }

        if (!lineNumbers.Order().SequenceEqual(Enumerable.Range(1, lines.Length)))
            throw Invalid("上限額管理結果票の行番号は1からの連番でなければなりません。");

        var totalCost = lines.Sum(line => (long)line.TotalCostYen.ValueYen!.Value);
        var totalPre = lines.Sum(line => (long)line.PreManagementBurdenYen.ValueYen!.Value);
        var totalManaged = lines.Sum(line => (long)line.ManagedBurdenYen.ValueYen!.Value);
        if (totalCost != headerTotalCost
            || totalPre != headerTotalPre
            || totalManaged != headerTotalManaged)
            throw Invalid("上限額管理結果票の行合計とheader合計が一致しません。");
        if (totalManaged > certificateCap)
            throw Invalid("上限額管理結果後合計が証上限を超えています。");

        var managingLines = lines
            .Where(line => string.Equals(line.OfficeNumber, statement.ManagingOfficeNumber,
                StringComparison.Ordinal))
            .ToArray();
        if (managingLines.Length != 1)
            throw Invalid("上限額管理結果票の管理事業所行は1件必要です。");

        switch (statement.Result)
        {
            case UpperLimitManagementResult.Result1:
                if (managingLines[0].ManagedBurdenYen.ValueYen != certificateCap
                    || totalManaged != certificateCap
                    || lines.Where(line => line != managingLines[0]).Any(line => line.ManagedBurdenYen.ValueYen != 0))
                    throw Invalid("上限額管理結果区分1の金額関係が不正です。");
                break;
            case UpperLimitManagementResult.Result2:
                if (lines.Any(line => line.ManagedBurdenYen.ValueYen != line.PreManagementBurdenYen.ValueYen)
                    || totalManaged != totalPre)
                    throw Invalid("上限額管理結果区分2の金額関係が不正です。");
                break;
            case UpperLimitManagementResult.Result3:
                if (totalPre <= certificateCap || totalManaged != certificateCap)
                    throw Invalid("上限額管理結果区分3の金額関係が不正です。");
                break;
            default:
                throw Invalid("上限額管理結果票の結果区分が不正です。");
        }
    }

    private static int RequireEntered(EnteredYen amount)
    {
        if (!amount.IsEntered || amount.ValueYen is not { } value)
            throw Invalid("上限額管理結果票の金額は入力済みでなければなりません。");
        return value;
    }

    private static void ValidateCancellation(UpperLimitManagementStatement statement)
    {
        if (!string.IsNullOrEmpty(statement.MunicipalityNumber)
            || !string.IsNullOrEmpty(statement.CertificateNumber)
            || statement.CertificateMonthlyCostCap.IsEntered
            || statement.CertificateMonthlyCostCap.ValueYen is not null
            || statement.UpperLimitManagementApplicability != UpperLimitManagementApplicability.Unknown
            || !string.IsNullOrEmpty(statement.CertificateManagingOfficeNumber)
            || !string.IsNullOrEmpty(statement.ManagingOfficeNumber)
            || !string.IsNullOrEmpty(statement.ManagingOfficeName)
            || !string.IsNullOrEmpty(statement.OriginalCreationKind)
            || statement.ReceivedAt is not null || statement.OriginalDocumentReference is not null
            || statement.IsConfirmed || statement.ConfirmedAt is not null
            || statement.ConfirmedBy is not null || statement.ConfirmationReason is not null
            || (int)statement.Result != 0
            || statement.TotalCostYen.IsEntered || statement.TotalCostYen.ValueYen is not null
            || statement.TotalPreManagementBurdenYen.IsEntered
            || statement.TotalPreManagementBurdenYen.ValueYen is not null
            || statement.TotalManagedBurdenYen.IsEntered
            || statement.TotalManagedBurdenYen.ValueYen is not null)
            throw Invalid("取消には上限額管理結果票の業務値を保持できません。");
    }

    private static InvalidOperationException Invalid(string message) => new(message);
}
