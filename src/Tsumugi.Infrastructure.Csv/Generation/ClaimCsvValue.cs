using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Csv.Generation;

/// <summary>
/// フィールド値の中間表現。最終的な文字列化は対象 <c>CsvFieldSpecification.DataType</c> に従うため、
/// ここでは型を保ったまま持ち回る（同じ <c>DailyRecord.ServiceDate</c> が
/// <c>date(8)=yyyyMMdd</c> にも <c>code(2)=日</c> にも写像されるため）。
/// </summary>
internal abstract record ClaimCsvValue
{
    public static ClaimCsvValue Missing { get; } = new AbsentValue();

    public static ClaimCsvValue FromNumber(long value) => new NumberValue(value);

    public static ClaimCsvValue FromText(string? value) =>
        string.IsNullOrEmpty(value) ? Missing : new TextValue(value);

    public static ClaimCsvValue FromDate(DateOnly value) => new DateValue(value);

    public static ClaimCsvValue FromMonth(ServiceMonth value) => new MonthValue(value);

    public static ClaimCsvValue FromTime(TimeOnly value) => new TimeValue(value);

    public static ClaimCsvValue FromOptional<T>(T? value, Func<T, ClaimCsvValue> project)
        where T : struct => value is { } present ? project(present) : Missing;

    public static ClaimCsvValue FromOptionalNumber(int? value) =>
        value is { } present ? FromNumber(present) : Missing;

    public bool IsAbsent => this is AbsentValue;

    public sealed record AbsentValue : ClaimCsvValue;

    public sealed record NumberValue(long Value) : ClaimCsvValue;

    public sealed record TextValue(string Value) : ClaimCsvValue;

    public sealed record DateValue(DateOnly Value) : ClaimCsvValue;

    public sealed record MonthValue(ServiceMonth Value) : ClaimCsvValue;

    public sealed record TimeValue(TimeOnly Value) : ClaimCsvValue;
}
