using System.Globalization;
using System.Text.Json;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

/// <summary>DateRange の SQLite 列への単一文字列シリアライズ。</summary>
internal static class DateRangeJson
{
    // NOTE: CLAUDE.md ハード制約 6 に従い Culture 依存を明示的に断つ。
    // globalization有効下でも実行環境のCultureに依存せず、ISO yyyy-MM-dd固定で
    // 読み書きされることを保証する。
    public static string Serialize(DateRange v) =>
        JsonSerializer.Serialize(new Dto(
            v.Start.ToString("O", CultureInfo.InvariantCulture),
            v.End?.ToString("O", CultureInfo.InvariantCulture)));

    public static DateRange Deserialize(string s)
    {
        var dto = JsonSerializer.Deserialize<Dto>(s)
            ?? throw new InvalidOperationException("DateRange のデシリアライズに失敗");
        return new DateRange(
            DateOnly.ParseExact(dto.Start, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            dto.End is null ? null : DateOnly.ParseExact(dto.End, "yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    private sealed record Dto(string Start, string? End);
}
