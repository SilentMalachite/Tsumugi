using FluentAssertions;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class PublishScriptContractTests
{
    [Fact]
    public void publish_sh_contains_fixed_osx_arm64_self_contained_contract()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "build", "publish.sh"));

        script.Should().Contain("osx-arm64");
        script.Should().Contain("dotnet publish src/Tsumugi.App");
        script.Should().Contain("-c Release");
        script.Should().Contain("--self-contained true");
        script.Should().Contain("-p:PublishSingleFile=true");
        script.Should().Contain("-p:PublishTrimmed=false");
        script.Should().Contain("artifacts/publish/osx-arm64");
    }

    [Fact]
    public void publish_ps1_contains_fixed_win_x64_self_contained_contract()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "build", "publish.ps1"));

        script.Should().Contain("win-x64");
        script.Should().Contain("dotnet publish src/Tsumugi.App");
        script.Should().Contain("-c Release");
        script.Should().Contain("--self-contained true");
        script.Should().Contain("-p:PublishSingleFile=true");
        script.Should().Contain("-p:PublishTrimmed=false");
        script.Should().Contain("artifacts/publish/win-x64");
    }

    [Fact]
    public void publish_scripts_abort_when_dotnet_publish_fails()
    {
        var root = FindRepositoryRoot();
        var sh = File.ReadAllText(Path.Combine(root, "build", "publish.sh"));
        var ps1 = File.ReadAllText(Path.Combine(root, "build", "publish.ps1"));

        sh.Should().Contain("set -euo pipefail",
            because: "失敗したビルドを成功として配布しない");

        // $ErrorActionPreference は Windows PowerShell 5.1 / PowerShell 7.0-7.2 では
        // ネイティブコマンドに適用されない。終了コードを自分で検査しないと
        // dotnet publish の失敗を検出できず、空の成果物が配布されうる。
        ps1.Should().Contain("$LASTEXITCODE",
            because: "native command の失敗は $LASTEXITCODE でしか検出できない");
        ps1.Should().MatchRegex(@"exit\s+\$LASTEXITCODE",
            because: "失敗をスクリプト自身の終了コードとして呼び出し側へ伝える");
    }

    [Fact]
    public void publish_scripts_tell_the_operator_to_copy_the_whole_output_directory()
    {
        var root = FindRepositoryRoot();
        var csproj = File.ReadAllText(
            Path.Combine(root, "src", "Tsumugi.App", "Tsumugi.App.csproj"));
        var sh = File.ReadAllText(Path.Combine(root, "build", "publish.sh"));
        var ps1 = File.ReadAllText(Path.Combine(root, "build", "publish.ps1"));

        // 単一ファイル発行でも IncludeNativeLibrariesForSelfExtract を立てていないため
        // ネイティブライブラリはサイドカーのまま出力され、NOTICE と
        // NotoSansJP.LICENSE.txt も CopyToPublishDirectory で実行ファイルの隣に並ぶ。
        // 実行ファイル1個だけをコピーすると起動失敗、またはライセンス欠落で配布される。
        csproj.Should().Contain("NOTICE");
        csproj.Should().Contain("NotoSansJP.LICENSE.txt");
        csproj.Should().Contain("<CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>");

        sh.Should().Contain("ディレクトリごと",
            because: "配布単位はディレクトリだと発行した本人に伝える");
        ps1.Should().Contain("ディレクトリごと",
            because: "配布単位はディレクトリだと発行した本人に伝える");
    }

    [Fact]
    public void gitignore_excludes_artifacts_directory()
    {
        var root = FindRepositoryRoot();
        var gitignore = File.ReadAllText(Path.Combine(root, ".gitignore"));

        gitignore.Should().Contain("artifacts/");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "Tsumugi.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
