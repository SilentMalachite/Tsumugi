using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests;

public sealed class RecipientContactFieldsTests
{
    [Fact]
    public void Create_defaults_disabilities_and_contact_fields_to_empty()
    {
        var r = Recipient.Create(
            Guid.NewGuid(), "氏", "シ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());

        r.Disabilities.Any.Should().BeFalse();
        r.PostalCode.Should().BeNull();
        r.Address.Should().BeNull();
        r.PhoneNumber.Should().BeNull();
        r.EmailAddress.Should().BeNull();
        r.EmergencyContactName.Should().BeNull();
        r.EmergencyContactRelationship.Should().BeNull();
        r.EmergencyContactPhone.Should().BeNull();
    }

    [Fact]
    public void Create_persists_all_optional_fields()
    {
        var r = Recipient.Create(
            Guid.NewGuid(), "氏", "シ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
            disabilities: new DisabilityCategories(true, false, true, false),
            postalCode: "100-0001", address: "東京都...",
            phoneNumber: "03-0000-0000", emailAddress: "x@example.com",
            emergencyContactName: "緊急一郎",
            emergencyContactRelationship: "兄",
            emergencyContactPhone: "090-0000-0000");

        r.Disabilities.Physical.Should().BeTrue();
        r.Disabilities.Mental.Should().BeTrue();
        r.Disabilities.Intellectual.Should().BeFalse();
        r.PostalCode.Should().Be("100-0001");
        r.PhoneNumber.Should().Be("03-0000-0000");
        r.EmergencyContactName.Should().Be("緊急一郎");
    }

    [Fact]
    public void Archive_preserves_disabilities_and_contact_fields()
    {
        var r = Recipient.Create(
            Guid.NewGuid(), "氏", "シ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
            disabilities: new DisabilityCategories(true, false, false, false),
            address: "東京都");

        var archived = r.Archive("admin", DateTimeOffset.UnixEpoch.AddDays(1));
        archived.Disabilities.Physical.Should().BeTrue();
        archived.Address.Should().Be("東京都");
    }
}
