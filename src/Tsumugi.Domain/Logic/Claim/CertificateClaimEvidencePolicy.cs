using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic.Claim;

/// <summary>受給者証請求根拠の追記型revision chainを検証し、実効版を解決する。</summary>
public static class CertificateClaimEvidencePolicy
{
    public static void ValidateHistory(IReadOnlyCollection<CertificateClaimEvidence> history)
    {
        ArgumentNullException.ThrowIfNull(history);
        if (history.Count == 0) return;

        var ordered = history.OrderBy(evidence => evidence.Revision).ToArray();
        var ids = new HashSet<Guid>();
        for (var index = 0; index < ordered.Length; index++)
        {
            if (!ids.Add(ordered[index].Id))
                throw Invalid("CertificateClaimEvidence履歴内でIDが重複しています。");
            if (ordered[index].Revision != index + 1)
                throw Invalid("CertificateClaimEvidenceのRevisionが欠落または重複しています。");
        }

        var root = ordered[0];
        if (root.Id == Guid.Empty || root.RootId == Guid.Empty || root.Id != root.RootId)
            throw Invalid("CertificateClaimEvidence Revision 1はroot自身でなければなりません。");
        if (root.Kind != RecordKind.New || root.ExpectedHeadId is not null)
            throw Invalid("CertificateClaimEvidence Revision 1はNewでexpected headを持てません。");
        if (root.CertificateId == Guid.Empty || root.Validity.Start == default)
            throw Invalid("CertificateClaimEvidenceのbusiness keyが不正です。");

        for (var index = 0; index < ordered.Length; index++)
        {
            var evidence = ordered[index];
            if (evidence.Id == Guid.Empty || evidence.RootId != root.Id)
                throw Invalid("CertificateClaimEvidenceのroot IDが一致しません。");
            if (evidence.CertificateId != root.CertificateId || evidence.Validity != root.Validity)
                throw Invalid("CertificateClaimEvidence履歴に異なる証または期間が混在しています。");
            if (evidence.Kind is not RecordKind.New and not RecordKind.Correct and not RecordKind.Cancel)
                throw Invalid("CertificateClaimEvidenceのRecordKindが不正です。");

            if (index > 0)
            {
                if (evidence.Kind == RecordKind.New)
                    throw Invalid("CertificateClaimEvidence Revision 1以外にNewを追加できません。");
                if (evidence.ExpectedHeadId is null
                    || evidence.ExpectedHeadId == Guid.Empty
                    || evidence.ExpectedHeadId != ordered[index - 1].Id)
                    throw Invalid("CertificateClaimEvidenceのexpected headが直前Revisionと一致しません。");
            }

            if (evidence.Kind == RecordKind.Cancel) ValidateCancellation(evidence);
            else ValidateEffectiveValue(evidence);
        }
    }

    public static CertificateClaimEvidence? Effective(
        IReadOnlyCollection<CertificateClaimEvidence> history)
    {
        ValidateHistory(history);
        if (history.Count == 0) return null;
        var head = history.MaxBy(evidence => evidence.Revision)!;
        return head.Kind == RecordKind.Cancel ? null : head;
    }

    public static int NextRevision(IReadOnlyCollection<CertificateClaimEvidence> history)
    {
        ValidateHistory(history);
        return history.Count == 0 ? 1 : checked(history.Max(evidence => evidence.Revision) + 1);
    }

    private static void ValidateEffectiveValue(CertificateClaimEvidence evidence)
    {
        if (!evidence.MonthlyCostCap.IsEntered || evidence.MonthlyCostCap.ValueYen is null)
            throw Invalid("受給者証上限は入力済みでなければなりません。");
        if (string.IsNullOrWhiteSpace(evidence.OriginalDocumentReference)
            || evidence.ConfirmedAt is null || evidence.ConfirmedAt == DateTimeOffset.MinValue
            || string.IsNullOrWhiteSpace(evidence.ConfirmedBy)
            || string.IsNullOrWhiteSpace(evidence.ConfirmationReason))
            throw Invalid("CertificateClaimEvidenceには原本参照と確認証跡が必要です。");

        if (!Enum.IsDefined(evidence.UpperLimitManagementApplicability)
            || evidence.UpperLimitManagementApplicability == UpperLimitManagementApplicability.Unknown)
            throw Invalid("上限額管理対象状態が未確認または不正です。");
        if (evidence.UpperLimitManagementApplicability == UpperLimitManagementApplicability.Applicable)
        {
            if (string.IsNullOrWhiteSpace(evidence.UpperLimitManagementOfficeNumber))
                throw Invalid("上限額管理対象には管理事業所番号が必要です。");
        }
        else if (evidence.UpperLimitManagementApplicability == UpperLimitManagementApplicability.NotApplicable
                 && evidence.UpperLimitManagementOfficeNumber is not null)
        {
            throw Invalid("上限額管理対象外に管理事業所番号を設定できません。");
        }

        if (!Enum.IsDefined(evidence.Article31Status)
            || evidence.Article31Status == Article31SpecialBurdenStatus.Unknown)
            throw Invalid("法31条特例状態が未確認または不正です。");
        if (evidence.Article31EffectivePeriod is not { } articlePeriod
            || !Contains(evidence.Validity, articlePeriod))
            throw Invalid("法31条特例には証の有効期間内の確認期間が必要です。");

        if (evidence.Article31Status == Article31SpecialBurdenStatus.Applicable)
        {
            if (!evidence.Article31AmountYen.IsEntered || evidence.Article31AmountYen.ValueYen is null)
                throw Invalid("適用される法31条特例には入力済み金額が必要です。");
        }
        else if (evidence.Article31AmountYen.IsEntered || evidence.Article31AmountYen.ValueYen is not null)
        {
            throw Invalid("法31条特例対象外に金額を設定できません。");
        }
    }

    private static bool Contains(DateRange outer, DateRange inner)
    {
        var outerEnd = outer.End ?? DateOnly.MaxValue;
        var innerEnd = inner.End ?? DateOnly.MaxValue;
        return inner.Start >= outer.Start && innerEnd <= outerEnd;
    }

    private static void ValidateCancellation(CertificateClaimEvidence evidence)
    {
        if (evidence.MonthlyCostCap.IsEntered || evidence.MonthlyCostCap.ValueYen is not null
            || evidence.UpperLimitManagementApplicability != UpperLimitManagementApplicability.Unknown
            || evidence.UpperLimitManagementOfficeNumber is not null
            || evidence.Article31Status != Article31SpecialBurdenStatus.Unknown
            || evidence.Article31AmountYen.IsEntered || evidence.Article31AmountYen.ValueYen is not null
            || evidence.Article31EffectivePeriod is not null || evidence.OriginalDocumentReference is not null
            || evidence.ConfirmedAt is not null || evidence.ConfirmedBy is not null
            || evidence.ConfirmationReason is not null)
            throw Invalid("取消には受給者証請求根拠の業務値を保持できません。");
    }

    private static InvalidOperationException Invalid(string message) => new(message);
}
