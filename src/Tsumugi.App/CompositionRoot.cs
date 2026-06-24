using Microsoft.Extensions.DependencyInjection;
using Tsumugi.Application.UseCases;
using Tsumugi.Infrastructure;

namespace Tsumugi.App;

/// <summary>アプリ全体のDI構成を一点に集約する合成ルート。テストからも同じ構成を再現できる。</summary>
public static class CompositionRoot
{
    public static IServiceProvider Build(string connectionString)
        => new ServiceCollection().AddTsumugiServices(connectionString).BuildServiceProvider();

    public static IServiceCollection AddTsumugiServices(
        this IServiceCollection services, string connectionString)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddTsumugiInfrastructure(connectionString);
        services.AddScoped<RegisterOfficeUseCase>();
        services.AddScoped<BackupDatabaseUseCase>();
        return services;
    }
}
