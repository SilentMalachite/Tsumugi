using System.Text.Json;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

/// <summary>DateRange の SQLite 列への単一文字列シリアライズ。</summary>
internal static class DateRangeJson
{
    public static string Serialize(DateRange v) =>
        JsonSerializer.Serialize(new Dto(v.Start.ToString("O"), v.End?.ToString("O")));

    public static DateRange Deserialize(string s)
    {
        var dto = JsonSerializer.Deserialize<Dto>(s)
            ?? throw new InvalidOperationException("DateRange のデシリアライズに失敗");
        return new DateRange(
            DateOnly.Parse(dto.Start),
            dto.End is null ? null : DateOnly.Parse(dto.End));
    }

    private sealed record Dto(string Start, string? End);
}
