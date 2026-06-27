using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

/// <summary>
/// R4-H1: アプリ全体で 1 つの Scope を維持する設計（App.axaml.cs:37）と
/// Scoped DbContext の組合せで、同一キーの再更新が EF Core の追跡衝突を起こさないこと。
/// UnitOfWork.SaveChangesAsync が ChangeTracker をクリアして
/// トランザクション境界として機能することを保証する。
/// </summary>
public sealed class RepositoryTrackingTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;
    public RepositoryTrackingTests(SqliteFixture f) => _fixture = f;

    [Fact]
    public async Task Office_can_be_updated_twice_via_repo_and_uow_without_tracking_conflict()
    {
        var id = Guid.NewGuid();
        await using var ctx = _fixture.NewContext();
        var repo = new OfficeRepository(ctx);
        var uow = new EfUnitOfWork(ctx);

        var initial = Office.Create(id, "1234567890", "初期",
            ServiceCategory.TypeB, RegionGrade.None, "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        await repo.AddAsync(initial, default);
        await uow.SaveChangesAsync(default);

        // 1 回目の更新
        var loaded1 = await repo.FindByIdAsync(id, default);
        var updated1 = loaded1! with { Name = "更新1" };
        await repo.UpdateAsync(updated1, default);
        await uow.SaveChangesAsync(default);

        // 2 回目の更新（同一 Id・新インスタンス）。追跡衝突せず通ること。
        var loaded2 = await repo.FindByIdAsync(id, default);
        var updated2 = loaded2! with { Name = "更新2" };
        Func<Task> act = async () =>
        {
            await repo.UpdateAsync(updated2, default);
            await uow.SaveChangesAsync(default);
        };

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Recipient_can_be_updated_twice_via_repo_and_uow_without_tracking_conflict()
    {
        var id = Guid.NewGuid();
        await using var ctx = _fixture.NewContext();
        var repo = new RecipientRepository(ctx);
        var uow = new EfUnitOfWork(ctx);

        var initial = Recipient.Create(id, "氏名", "シメイ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        await repo.AddAsync(initial, default);
        await uow.SaveChangesAsync(default);

        var loaded1 = await repo.FindByIdAsync(id, default);
        await repo.UpdateAsync(loaded1! with { KanjiName = "新名1" }, default);
        await uow.SaveChangesAsync(default);

        var loaded2 = await repo.FindByIdAsync(id, default);
        Func<Task> act = async () =>
        {
            await repo.UpdateAsync(loaded2! with { KanjiName = "新名2" }, default);
            await uow.SaveChangesAsync(default);
        };

        await act.Should().NotThrowAsync();
    }
}
