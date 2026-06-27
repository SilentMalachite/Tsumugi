namespace Tsumugi.Domain.Enums;

/// <summary>
/// 性別。受給者証「支給決定障害者等」欄の必須項目。
/// 国保連請求 CSV でも符号化が必要。MHLW 公式様式では「男 / 女」のみ。
/// 区分外の運用が必要な事業所向けに <see cref="Other"/> を予備として持つが、
/// 既定運用は MHLW 様式の二値に従う。
/// </summary>
public enum Gender
{
    Unspecified = 0,
    Male = 1,
    Female = 2,
    Other = 9,
}
