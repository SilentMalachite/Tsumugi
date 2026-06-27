namespace Tsumugi.Domain.Enums;

/// <summary>
/// 障害者手帳の種類。法律ごとに発行根拠と等級体系が異なる。
///   - 身体: 身体障害者福祉法 (1〜6級、種別 1種/2種)
///   - 療育: 厚生事務次官通知 (各都道府県により A/B、1〜4度 等、表記が異なる)
///   - 精神: 精神保健福祉法 (1〜3級、2年ごと更新)
/// 難病等は手帳ではなく受給者証で支援を受けるためここには含めない。
/// </summary>
public enum DisabilityCertificateType
{
    /// <summary>身体障害者手帳。</summary>
    Physical = 1,
    /// <summary>療育手帳 (自治体により「愛の手帳」「みどりの手帳」等)。</summary>
    Intellectual = 2,
    /// <summary>精神障害者保健福祉手帳 (2年ごとの更新が必要)。</summary>
    Mental = 3,
}
