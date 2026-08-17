namespace Tsumugi.Domain.Logic;

/// <summary>初回起動が必要かを事業所件数から判定する純粋関数。</summary>
public static class FirstRunPolicy
{
    /// <summary>
    /// 事業所が未登録なら初回登録が必要。
    /// 件数は COUNT(*) 由来なので負数は起こらないが、境界を跨いだときに
    /// 「未登録」側へ倒れるよう <c>&lt; 1</c> で書く（例外は投げない）。
    /// </summary>
    public static bool NeedsFirstRun(int officeCount) => officeCount < 1;
}
