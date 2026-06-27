namespace Tsumugi.Domain.ValueObjects;

/// <summary>両端含む暦日範囲 [Start, End]。End=null は継続中（無期限）。</summary>
public readonly record struct DateRange
{
    public DateOnly Start { get; }
    public DateOnly? End { get; }

    public DateRange(DateOnly Start, DateOnly? End)
    {
        if (End is { } e && e < Start)
            throw new ArgumentException("終了日は開始日以降である必要があります。", nameof(End));
        this.Start = Start;
        this.End = End;
    }

    public bool Contains(DateOnly d) => d >= Start && (End is null || d <= End);

    public bool Overlaps(DateRange other)
    {
        var aEnd = End ?? DateOnly.MaxValue;
        var bEnd = other.End ?? DateOnly.MaxValue;
        return Start <= bEnd && other.Start <= aEnd;
    }
}
