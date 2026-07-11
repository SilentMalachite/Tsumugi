using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

/// <summary>検証や実効版選択を行わない、永続化された請求headerと全detailsの組。</summary>
public sealed record ClaimBatchAggregate
{
    public ClaimBatchAggregate(ClaimBatch header, IEnumerable<ClaimDetail> details)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(details);
        Header = header;
        Details = details.ToArray();
    }

    public ClaimBatch Header { get; }
    public IReadOnlyList<ClaimDetail> Details { get; }
}
