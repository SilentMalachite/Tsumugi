using System;
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class OfficeTests
{
    [Fact]
    public void Create_sets_all_fields()
    {
        var id = Guid.NewGuid();
        var token = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.FromHours(9));

        var office = Office.Create(id, "1234567890", "つむぎ作業所", "tester", at, token);

        office.Id.Should().Be(id);
        office.OfficeNumber.Should().Be("1234567890");
        office.Name.Should().Be("つむぎ作業所");
        office.CreatedBy.Should().Be("tester");
        office.CreatedAt.Should().Be(at);
        office.ConcurrencyToken.Should().Be(token);
    }

    [Fact]
    public void Records_with_same_values_are_equal()
    {
        var id = Guid.NewGuid();
        var token = Guid.NewGuid();
        var at = DateTimeOffset.UnixEpoch;

        var a = Office.Create(id, "1", "x", "u", at, token);
        var b = Office.Create(id, "1", "x", "u", at, token);

        a.Should().Be(b); // record の値等価性
    }
}
