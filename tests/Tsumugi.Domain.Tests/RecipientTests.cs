using FluentAssertions;
using Tsumugi.Domain.Entities;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class RecipientTests
{
    [Fact]
    public void Create_sets_all_fields()
    {
        var id = Guid.NewGuid();
        var token = Guid.NewGuid();
        var at = DateTimeOffset.UnixEpoch;
        var dob = new DateOnly(1990, 1, 1);

        var r = Recipient.Create(id, "山田太郎", "ヤマダタロウ", dob, "u", at, token);

        r.Id.Should().Be(id);
        r.KanjiName.Should().Be("山田太郎");
        r.KanaName.Should().Be("ヤマダタロウ");
        r.DateOfBirth.Should().Be(dob);
        r.CreatedBy.Should().Be("u");
        r.CreatedAt.Should().Be(at);
        r.ConcurrencyToken.Should().Be(token);
    }
}
