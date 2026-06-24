using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tsumugi.Application.Abstractions;
using Tsumugi.Infrastructure.Persistence;

namespace Tsumugi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTsumugiInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<TsumugiDbContext>(o => o.UseSqlite(connectionString));
        services.AddScoped<IOfficeRepository, OfficeRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        // TODO(Task 6): enable backup registration
        // services.AddScoped<IBackupService, SqliteBackupService>();
        return services;
    }
}
