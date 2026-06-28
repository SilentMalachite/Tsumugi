using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

/// <summary>
/// マイグレーション AddRecipientDisabilitiesAndContact による Disabilities / 連絡先 列を
/// 実 SQLite で往復確認する。ComplexProperty による Disability_* 4 列展開も検証する。
/// </summary>
public sealed class RecipientContactRoundTripTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;
    public RecipientContactRoundTripTests(SqliteFixture f) => _fixture = f;

    [Fact]
    public async Task Recipient_with_disabilities_and_contact_round_trips()
    {
        var id = Guid.NewGuid();
        await using (var ctx = _fixture.NewContext())
        {
            ctx.Recipients.Add(Recipient.Create(
                id, "往復名", "オウフクメイ",
                new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
                disabilities: new DisabilityCategories(true, false, true, false),
                postalCode: "100-0001",
                address: "東京都千代田区...",
                phoneNumber: "03-1111-2222",
                emailAddress: "x@example.com",
                emergencyContactName: "緊急一郎",
                emergencyContactRelationship: "兄",
                emergencyContactPhone: "090-1234-5678"));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _fixture.NewContext())
        {
            var loaded = await ctx.Recipients.AsNoTracking().FirstAsync(r => r.Id == id);
            loaded.Disabilities.Physical.Should().BeTrue();
            loaded.Disabilities.Mental.Should().BeTrue();
            loaded.Disabilities.Intellectual.Should().BeFalse();
            loaded.Disabilities.Intractable.Should().BeFalse();
            loaded.PostalCode.Should().Be("100-0001");
            loaded.Address.Should().Be("東京都千代田区...");
            loaded.PhoneNumber.Should().Be("03-1111-2222");
            loaded.EmailAddress.Should().Be("x@example.com");
            loaded.EmergencyContactName.Should().Be("緊急一郎");
            loaded.EmergencyContactRelationship.Should().Be("兄");
            loaded.EmergencyContactPhone.Should().Be("090-1234-5678");
        }
    }
}
