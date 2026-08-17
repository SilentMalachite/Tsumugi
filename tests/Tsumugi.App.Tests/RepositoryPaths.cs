namespace Tsumugi.App.Tests;

/// <summary>
/// 構造テストが参照するリポジトリ内パスの解決を1箇所に集約する。
/// 各テストが独自に walk-up を書くと、判定方法も例外型も食い違ったまま増える。
/// </summary>
internal static class RepositoryPaths
{
    /// <summary><c>Tsumugi.sln</c> を含むディレクトリ。</summary>
    public static string Root { get; } = FindRoot();

    /// <summary><c>src/Tsumugi.App</c>。</summary>
    public static string AppProject { get; } = Path.Combine(Root, "src", "Tsumugi.App");

    /// <summary><c>src/Tsumugi.App/Views</c>。</summary>
    public static string AppViews { get; } = Path.Combine(AppProject, "Views");

    public static string View(string fileName) => Path.Combine(AppViews, fileName);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Tsumugi.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Tsumugi.sln が祖先方向に見つからない");
    }
}
