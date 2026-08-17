namespace Tsumugi.Domain.Logic;

/// <summary>初回起動が必要かを事業所件数から判定する純粋関数。</summary>
public static class FirstRunPolicy
{
    /// <summary>
    /// 事業所が未登録（件数 1 未満）なら初回登録が必要。
    /// 負数も未登録扱いとし、例外は投げない。
    /// </summary>
    public static bool NeedsFirstRun(int officeCount) => officeCount < 1;
}
