using FluentAssertions;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class PublishScriptContractTests
{
    [Fact]
    public void publish_sh_contains_fixed_osx_arm64_self_contained_contract()
    {
        var root = RepositoryPaths.Root;
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
        var root = RepositoryPaths.Root;
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
        var root = RepositoryPaths.Root;
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
        var root = RepositoryPaths.Root;
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
    public void gitignore_excludes_exactly_the_publish_output_directory_named_by_adr_0054()
    {
        var root = RepositoryPaths.Root;
        var entries = File.ReadAllLines(Path.Combine(root, ".gitignore"))
            .Select(line => line.Trim())
            .ToArray();

        entries.Should().Contain("artifacts/publish/",
            because: "ADR 0054 決定2 が除外対象として名指ししているのはこの1ディレクトリ");

        // artifacts/ は .NET SDK の既定 ArtifactsPath ルートでもある。ツリー全体を
        // 無視すると、将来そこへ置いた追跡対象がエラーも無く commit から落ちる。
        entries.Should().NotContain("artifacts/",
            because: "除外はADRが名指しした範囲に留める");
    }

}
