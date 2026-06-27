namespace Tsumugi.Domain.Enums;

/// <summary>
/// 利用者負担に関する事項：負担区分。
/// 障害者総合支援法に基づく利用者負担上限月額の所得区分。
/// 各区分の月額上限（円）は告示で別途定義。
/// </summary>
public enum PaymentBurdenCategory
{
    Unspecified = 0,
    /// <summary>生活保護受給世帯（月額上限 0 円）。</summary>
    Welfare = 1,
    /// <summary>低所得（市町村民税非課税世帯、月額上限 0 円）。</summary>
    LowIncome = 2,
    /// <summary>一般1（市町村民税課税世帯のうち所得割16万円未満等）。</summary>
    General1 = 3,
    /// <summary>一般2（上記以外）。</summary>
    General2 = 4,
}
