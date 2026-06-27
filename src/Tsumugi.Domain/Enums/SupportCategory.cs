namespace Tsumugi.Domain.Enums;

/// <summary>
/// 障害支援区分。市町村が認定する 1〜6 の区分（区分が高いほど支援が必要）。
/// 訓練等給付（就労継続支援B型 等）では区分認定が必須でない場合があり、
/// その場合は <see cref="None"/>（区分なし）を用いる。
/// </summary>
public enum SupportCategory
{
    None = 0,
    Category1 = 1,
    Category2 = 2,
    Category3 = 3,
    Category4 = 4,
    Category5 = 5,
    Category6 = 6,
}
