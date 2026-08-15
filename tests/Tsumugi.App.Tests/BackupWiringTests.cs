using System;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Backup;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class BackupWiringTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "tsumugi-wiring-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Backup_and_restore_services_resolve_from_the_location_aware_composition_root()
    {
        var location = new SqliteLocationService(_root);
        location.EnsureSecuredStorage();

        var services = CompositionRoot.Build(location);
        using var scope = services.CreateScope();

        scope.ServiceProvider.GetRequiredService<RunScheduledBackupUseCase>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<RestoreDatabaseUseCase>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ListBackupGenerationsUseCase>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IBackupDirectory>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IDatabaseRestoreService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IDatabaseFileLocation>()
            .BackupDirectory.Should().Be(Path.Combine(_root, "backups"));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
