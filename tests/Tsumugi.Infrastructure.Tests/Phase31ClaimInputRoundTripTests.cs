using System.Globalization;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Infrastructure.Persistence;

namespace Tsumugi.Infrastructure.Tests;

public sealed class Phase31ClaimInputRoundTripTests
{
    [Fact]
    public async Task Claim_input_columns_have_official_nullability_and_max_lengths()
    {
        await using var database = await OpenDatabaseAsync();
        await using var context = NewContext(database);
        var office = context.Model.FindEntityType(typeof(Office));
        var provider = context.Model.FindEntityType(typeof(ContractedProvider));

        using var scope = new AssertionScope();
        office!.FindProperty(nameof(Office.PostalCode))!.GetMaxLength().Should().Be(16);
        office.FindProperty(nameof(Office.Address))!.GetMaxLength().Should().Be(256);
        office.FindProperty(nameof(Office.PhoneNumber))!.GetMaxLength().Should().Be(32);
        office.FindProperty(nameof(Office.RepresentativeTitleAndName))!
            .GetMaxLength().Should().Be(128);
        provider!.FindProperty(nameof(ContractedProvider.CertificateEntryNumber))!
            .IsNullable.Should().BeTrue();
    }

    [Fact]
    public async Task Office_and_provider_claim_inputs_round_trip_through_repositories()
    {
        await using var database = await OpenDatabaseAsync();
        var officeId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        await using (var context = NewContext(database))
        {
            var offices = new OfficeRepository(context);
            var providers = new ContractedProviderRepository(context);
            await offices.AddAsync(Office.Create(
                officeId, "1234567890", "つむぎ", ServiceCategory.TypeB, RegionGrade.Grade4,
                "tester", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
                postalCode: "100-0001", address: "東京都千代田区1-1",
                phoneNumber: "03-1234-5678", representativeTitleAndName: "代表 山田太郎"),
                default);
            await providers.AddAsync(ContractedProvider.Create(
                providerId, Guid.NewGuid(), "0987654321", "契約事業所", "就労継続支援B型",
                22, new DateOnly(2026, 4, 1), "tester", DateTimeOffset.UnixEpoch,
                Guid.NewGuid(), certificateEntryNumber: 0), default);
            await context.SaveChangesAsync();
        }

        await using (var context = NewContext(database))
        {
            var office = await new OfficeRepository(context).FindByIdAsync(officeId, default);
            var provider = await new ContractedProviderRepository(context)
                .FindByIdAsync(providerId, default);

            office!.PostalCode.Should().Be("100-0001");
            office.Address.Should().Be("東京都千代田区1-1");
            office.PhoneNumber.Should().Be("03-1234-5678");
            office.RepresentativeTitleAndName.Should().Be("代表 山田太郎");
            provider!.CertificateEntryNumber.Should().Be(0);
        }
    }

    [Fact]
    public async Task Office_repository_rejects_stale_claim_input_update()
    {
        await using var database = await OpenDatabaseAsync();
        var id = Guid.NewGuid();
        await using (var seed = NewContext(database))
        {
            await new OfficeRepository(seed).AddAsync(Office.Create(
                id, "1111111111", "つむぎ", ServiceCategory.TypeB, RegionGrade.None,
                "tester", DateTimeOffset.UnixEpoch, Guid.NewGuid()), default);
            await seed.SaveChangesAsync();
        }

        await AssertStaleUpdateAsync(
            database,
            async context => await new OfficeRepository(context).FindByIdAsync(id, default),
            async (context, office, value) =>
            {
                await new OfficeRepository(context).UpdateAsync(
                    office with { PostalCode = value }, default);
            });
    }

    [Fact]
    public async Task Provider_repository_rejects_stale_certificate_entry_update()
    {
        await using var database = await OpenDatabaseAsync();
        var id = Guid.NewGuid();
        await using (var seed = NewContext(database))
        {
            await new ContractedProviderRepository(seed).AddAsync(ContractedProvider.Create(
                id, Guid.NewGuid(), "2222222222", "契約事業所", "就労継続支援B型", 22,
                new DateOnly(2026, 4, 1), "tester", DateTimeOffset.UnixEpoch,
                Guid.NewGuid()), default);
            await seed.SaveChangesAsync();
        }

        await AssertStaleUpdateAsync(
            database,
            async context => await new ContractedProviderRepository(context)
                .FindByIdAsync(id, default),
            async (context, provider, value) =>
            {
                await new ContractedProviderRepository(context).UpdateAsync(
                    provider with
                    {
                        CertificateEntryNumber = int.Parse(value, CultureInfo.InvariantCulture),
                    }, default);
            }, "1", "2");
    }

    private static async Task AssertStaleUpdateAsync<TEntity>(
        SqliteConnection database,
        Func<TsumugiDbContext, Task<TEntity?>> load,
        Func<TsumugiDbContext, TEntity, string, Task> update,
        string winningValue = "100-0001",
        string staleValue = "200-0002")
        where TEntity : Entity
    {
        await using var winningContext = NewContext(database);
        await using var staleContext = NewContext(database);
        var winner = await load(winningContext);
        var stale = await load(staleContext);

        await update(winningContext, winner!, winningValue);
        await winningContext.SaveChangesAsync();

        await update(staleContext, stale!, staleValue);
        var act = () => staleContext.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    private static async Task<SqliteConnection> OpenDatabaseAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.EnsureCreatedAsync();
        return connection;
    }

    private static TsumugiDbContext NewContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<TsumugiDbContext>()
            .UseSqlite(connection)
            .Options;
        return new TsumugiDbContext(options);
    }
}
