using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

/// <summary>検証や実効版選択を行わない、永続化された上限額管理結果票headerと全linesの組。</summary>
public sealed record UpperLimitManagementStatementAggregate
{
    public UpperLimitManagementStatementAggregate(
        UpperLimitManagementStatement header,
        IEnumerable<UpperLimitManagementStatementLine> lines)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(lines);
        Header = header;
        Lines = lines.OrderBy(line => line.LineNumber).ToArray();
    }

    public UpperLimitManagementStatement Header { get; }
    public IReadOnlyList<UpperLimitManagementStatementLine> Lines { get; }
}
