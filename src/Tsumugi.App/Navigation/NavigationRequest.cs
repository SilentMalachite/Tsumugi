using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.Navigation;

/// <summary>対象画面へ渡せる最小限のナビゲーション文脈。</summary>
public sealed record NavigationRequest(
    AppSection Section,
    Guid? RecipientId = null,
    DateOnly? ServiceDate = null,
    Guid? CertificateId = null,
    Guid? OfficeId = null,
    ServiceMonth? ServiceMonth = null);
