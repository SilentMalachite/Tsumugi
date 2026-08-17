using FluentAssertions;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class RepositoryPathsTests
{
    [Fact]
    public void Repository_root_is_located_by_exactly_one_helper_in_this_assembly()
    {
        // 同じ4行の walk-up がテストアセンブリ内に6実装あり、判定方法
        // （EnumerateFiles / File.Exists）も投げる例外型
        // （InvalidOperationException / DirectoryNotFoundException）も食い違っていた。
        // リポジトリ構成を変えたときに直す箇所が6つに散る。
        var testSourceDir = Path.Combine(RepositoryPaths.Root, "tests", "Tsumugi.App.Tests");

        // walk-up は必ず AppContext.BaseDirectory から始まる。これを起点にする
        // ファイルが複数あれば、ルート解決がまた散り始めたということ。
        var definers = Directory
            .EnumerateFiles(testSourceDir, "*.cs", SearchOption.AllDirectories)
            // 検査そのものが needle を含むので、この検査ファイルは対象から外す。
            .Where(file => Path.GetFileName(file) != "RepositoryPathsTests.cs")
            .Where(file => File.ReadAllText(file)
                .Contains("AppContext.BaseDirectory", StringComparison.Ordinal))
            .Select(file => Path.GetFileName(file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        definers.Should().Equal(["RepositoryPaths.cs"],
            because: "リポジトリルートの解決は1箇所に集約する。実際の解決結果: "
                     + string.Join(", ", definers));
    }

    [Fact]
    public void Root_points_at_the_directory_containing_the_solution()
    {
        File.Exists(Path.Combine(RepositoryPaths.Root, "Tsumugi.sln")).Should().BeTrue();
    }
}
