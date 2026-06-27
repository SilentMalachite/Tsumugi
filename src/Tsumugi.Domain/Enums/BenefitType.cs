namespace Tsumugi.Domain.Enums;

/// <summary>
/// 給付種別。受給者証「支給決定の内容」欄の最上位区分。
/// 障害者総合支援法における支給決定の体系に対応する。
/// </summary>
public enum BenefitType
{
    /// <summary>介護給付（居宅介護・重度訪問介護 等）。</summary>
    Care = 1,
    /// <summary>訓練等給付（就労移行・就労継続A/B・自立訓練 等）。Tsumugi の主対象。</summary>
    Training = 2,
    /// <summary>障害児通所支援（児童発達支援・放課後等デイサービス 等）。</summary>
    ChildSupport = 3,
}
