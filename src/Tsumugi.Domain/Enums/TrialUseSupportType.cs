namespace Tsumugi.Domain.Enums;

/// <summary>
/// 体験利用支援加算の内部区分。公式コードへの変換は制度マスタresolverが行う。
/// </summary>
public enum TrialUseSupportType
{
    Unspecified = 0,
    TypeI = 1,
    TypeII = 2,
}
