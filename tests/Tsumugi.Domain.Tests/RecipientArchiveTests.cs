using FluentAssertions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Domain.Tests;

public sealed class RecipientArchiveTests
{
    private static Recipient New() => Recipient.Create(
        Guid.NewGuid(), "原典太郎", "ゲンテンタロウ",
        new DateOnly(1980, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());

    [Fact]
    public void Create_initialises_archive_fields_to_null()
    {
        var r = New();
        r.ArchivedAt.Should().BeNull();
        r.ArchivedBy.Should().BeNull();
        r.IsArchived.Should().BeFalse();
    }

    [Fact]
    public void Archive_sets_metadata_and_flips_IsArchived()
    {
        var r = New();
        var when = new DateTimeOffset(2026, 6, 28, 9, 0, 0, TimeSpan.Zero);
        var archived = r.Archive("operator", when);

        archived.IsArchived.Should().BeTrue();
        archived.ArchivedAt.Should().Be(when);
        archived.ArchivedBy.Should().Be("operator");
        // 元のレコードは不変（record の with パターン）。
        r.IsArchived.Should().BeFalse();
    }

    [Fact]
    public void Archive_is_idempotent_when_already_archived()
    {
        var first = New().Archive("first", DateTimeOffset.UnixEpoch);
        var second = first.Archive("second", DateTimeOffset.UnixEpoch.AddDays(1));
        second.Should().BeSameAs(first);
    }

    [Fact]
    public void Restore_clears_archive_metadata()
    {
        var archived = New().Archive("u", DateTimeOffset.UnixEpoch);
        var restored = archived.Restore();
        restored.IsArchived.Should().BeFalse();
        restored.ArchivedAt.Should().BeNull();
        restored.ArchivedBy.Should().BeNull();
    }

    [Fact]
    public void Restore_on_active_returns_same_instance()
    {
        var r = New();
        r.Restore().Should().BeSameAs(r);
    }
}
