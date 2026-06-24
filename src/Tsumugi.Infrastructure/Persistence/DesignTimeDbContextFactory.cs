using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TsumugiDbContext>
{
    public TsumugiDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TsumugiDbContext>()
            .UseSqlite("Data Source=design.db").Options;
        return new TsumugiDbContext(options);
    }
}
