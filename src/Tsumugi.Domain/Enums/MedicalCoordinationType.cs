namespace Tsumugi.Domain.Enums;

/// <summary>
/// 医療連携体制加算の内部区分。公式コードへの変換は制度マスタresolverが行う。
/// </summary>
public enum MedicalCoordinationType
{
    Unspecified = 0,
    TypeI = 1,
    TypeII = 2,
    TypeIII = 3,
    TypeIV = 4,
    TypeV = 5,
    TypeVI = 6,
}
