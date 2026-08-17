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
