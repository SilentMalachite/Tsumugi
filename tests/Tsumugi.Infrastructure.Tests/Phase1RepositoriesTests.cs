using System;
using System.Threading.Tasks;
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class Phase1RepositoriesTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;

    public Phase1RepositoriesTests(SqliteFixture f) => _fixture = f;

    [Fact]
    public async Task CertificateRepository_FindEffective_returns_in_range()
    {
        var rid = Guid.NewGuid();
        await using var ctx = _fixture.NewContext();
        var repo = new CertificateRepository(ctx);

        await repo.AddAsync(Certificate.Create(
            Guid.NewGuid(), rid, "1",
            new DateRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31)),
            0, 0, "x", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()), default);
        await repo.AddAsync(Certificate.Create(
            Guid.NewGuid(), rid, "2",
            new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
            0, 0, "x", "u", DateTimeOffset.UnixEpoch.AddSeconds(1), Guid.NewGuid()), default);
        await ctx.SaveChangesAsync();

        var hit = await repo.FindEffectiveAsync(rid, new DateOnly(2026, 5, 1), default);
        hit.Should().NotBeNull();
        hit!.CertificateNumber.Should().Be("2");
    }

    [Fact]
    public async Task CertificateRepository_FindEffective_returns_null_outside_range()
    {
        var rid = Guid.NewGuid();
        await using var ctx = _fixture.NewContext();
        var repo = new CertificateRepository(ctx);

        await repo.AddAsync(Certificate.Create(
            Guid.NewGuid(), rid, "A",
            new DateRange(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)),
            0, 0, "x", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()), default);
        await ctx.SaveChangesAsync();

        var miss = await repo.FindEffectiveAsync(rid, new DateOnly(2026, 1, 1), default);
        miss.Should().BeNull();
    }
}
