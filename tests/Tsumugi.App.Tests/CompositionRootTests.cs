using System;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tsumugi.App;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class CompositionRootTests
{
    [Fact]
    public void Build_resolves_use_cases_from_root()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"tsumugi-ci-{Guid.NewGuid():N}.db");
        try
        {
            using var provider = (ServiceProvider)CompositionRoot.Build($"Data Source={dbPath}");
            using var scope = provider.CreateScope();

            scope.ServiceProvider.GetRequiredService<RegisterOfficeUseCase>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<BackupDatabaseUseCase>().Should().NotBeNull();
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var f in new[] { dbPath, dbPath + "-shm", dbPath + "-wal" })
                if (File.Exists(f)) File.Delete(f);
        }
    }

    [Fact]
    public void Infrastructure_is_swappable_via_service_collection()
    {
        // App は IOfficeRepository を抽象で消費する。テストで差し替え可能であることを示す。
        var services = new ServiceCollection().AddTsumugiServices("Data Source=:memory:");
        var fake = new FakeRepo();
        services.AddScoped<IOfficeRepository>(_ => fake);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IOfficeRepository>().Should().BeSameAs(fake);
    }

    private sealed class FakeRepo : IOfficeRepository
    {
        public System.Threading.Tasks.Task AddAsync(Tsumugi.Domain.Entities.Office o, System.Threading.CancellationToken ct)
            => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task<Tsumugi.Domain.Entities.Office?> FindByNumberAsync(string n, System.Threading.CancellationToken ct)
            => System.Threading.Tasks.Task.FromResult<Tsumugi.Domain.Entities.Office?>(null);
    }
}
