using FluentAssertions;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Persistence.Configurations;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class DateRangeJsonTests
{
    // globalization有効下でも「ISO文字列で書き出され、ISO文字列から元の値に戻る」契約を
    // ピン留めし、実行環境のCultureに依存しない既定値を固定する。

    [Fact]
    public void Serialize_emits_iso_yyyy_MM_dd_for_start_and_end()
    {
        var range = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31));
        var json = DateRangeJson.Serialize(range);
        json.Should().Contain("\"Start\":\"2026-04-01\"");
        json.Should().Contain("\"End\":\"2027-03-31\"");
    }

    [Fact]
    public void Serialize_emits_null_end_for_open_ended_range()
    {
        var range = new DateRange(new DateOnly(2026, 4, 1), null);
        var json = DateRangeJson.Serialize(range);
        json.Should().Contain("\"Start\":\"2026-04-01\"");
        json.Should().Contain("\"End\":null");
    }

    [Fact]
    public void Round_trip_preserves_bounded_range()
    {
        var input = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31));
        var back = DateRangeJson.Deserialize(DateRangeJson.Serialize(input));
        back.Should().Be(input);
    }

    [Fact]
    public void Round_trip_preserves_open_ended_range()
    {
        var input = new DateRange(new DateOnly(2026, 4, 1), null);
        var back = DateRangeJson.Deserialize(DateRangeJson.Serialize(input));
        back.Should().Be(input);
    }
}
