namespace Tsumugi.Domain.ValueObjects;

/// <summary>
/// 障害種別の複数チェック値。受給者証「障害種別」欄に対応。
/// 1 名の利用者が複数種別を併有しうるため、フラグ的に保持する。
/// </summary>
public readonly record struct DisabilityCategories(
    bool Physical,
    bool Intellectual,
    bool Mental,
    bool Intractable)
{
    public static DisabilityCategories None => default;

    public bool Any => Physical || Intellectual || Mental || Intractable;
}
