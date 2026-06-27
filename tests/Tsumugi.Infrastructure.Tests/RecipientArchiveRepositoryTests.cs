using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

/// <summary>
/// マイグレーション（ArchivedAt/ArchivedBy 列）＋ Repository.ListAsync(includeArchived) の挙動を
/// 実 SQLite 上で検証する。SqliteFixture は IClassFixture のためクラス内で DB を共有する
/// 仕様なので、各テストはユニークな ID/名前で識別して断定する。
/// </summary>
public sealed class RecipientArchiveRepositoryTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;
    public RecipientArchiveRepositoryTests(SqliteFixture f) => _fixture = f;

    [Fact]
    public async Task ListAsync_excludes_archived_by_default_and_includes_when_requested()
    {
        await using var ctx = _fixture.NewContext();
        var repo = new RecipientRepository(ctx);
        var uow = new EfUnitOfWork(ctx);

        var active = Recipient.Create(Guid.NewGuid(), "あいうえ-アクティブ", "テストアクティブ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        var archived = Recipient.Create(Guid.NewGuid(), "かきくけ-アーカイブ", "テストアーカイブ",
            new DateOnly(1980, 5, 5), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid())
            .Archive("u", new DateTimeOffset(2026, 6, 28, 9, 0, 0, TimeSpan.Zero));
        await repo.AddAsync(active, default);
        await repo.AddAsync(archived, default);
        await uow.SaveChangesAsync(default);

        var defaultList = await repo.ListAsync(includeArchived: false, default);
        defaultList.Should().Contain(r => r.Id == active.Id);
        defaultList.Should().NotContain(r => r.Id == archived.Id);

        var fullList = await repo.ListAsync(includeArchived: true, default);
        fullList.Should().Contain(r => r.Id == active.Id);
        fullList.Should().Contain(r => r.Id == archived.Id);
    }

    [Fact]
    public async Task Archive_then_Restore_round_trip_preserves_other_fields()
    {
        await using var ctx = _fixture.NewContext();
        var repo = new RecipientRepository(ctx);
        var uow = new EfUnitOfWork(ctx);

        var id = Guid.NewGuid();
        var seed = Recipient.Create(id, "往復名", "オウフクメイ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        await repo.AddAsync(seed, default);
        await uow.SaveChangesAsync(default);

        var loaded = await repo.FindByIdAsync(id, default);
        var when = new DateTimeOffset(2026, 6, 28, 9, 0, 0, TimeSpan.Zero);
        await repo.UpdateAsync(loaded!.Archive("operator", when), default);
        await uow.SaveChangesAsync(default);

        var afterArchive = await repo.FindByIdAsync(id, default);
        afterArchive!.IsArchived.Should().BeTrue();
        afterArchive.ArchivedAt.Should().Be(when);
        afterArchive.ArchivedBy.Should().Be("operator");
        afterArchive.KanjiName.Should().Be("往復名");

        await repo.UpdateAsync(afterArchive.Restore(), default);
        await uow.SaveChangesAsync(default);
        var afterRestore = await repo.FindByIdAsync(id, default);
        afterRestore!.IsArchived.Should().BeFalse();
        afterRestore.ArchivedAt.Should().BeNull();
        afterRestore.ArchivedBy.Should().BeNull();
    }
}
