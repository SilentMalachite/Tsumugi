using System.Collections.ObjectModel;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>
/// 事業所体制（期間マスタ・実効日付つき追記）。加算フラグは拡張可能なコード集合（文字列キー）。
/// ★要・報酬告示突合（暫定）: フラグキーの正式値・単位数はフェーズ3で確定。
/// </summary>
public sealed record OfficeCapability : Entity
{
    public required Guid OfficeId { get; init; }
    public required DateRange Period { get; init; }
    public required IReadOnlyDictionary<string, bool> Flags { get; init; }

    public static OfficeCapability Create(
        Guid id, Guid officeId, DateRange period,
        IReadOnlyDictionary<string, bool> flags,
        string createdBy, DateTimeOffset createdAt, Guid concurrencyToken)
    {
        ArgumentNullException.ThrowIfNull(flags);
        var copy = new ReadOnlyDictionary<string, bool>(new Dictionary<string, bool>(flags));
        return new()
        {
            Id = id,
            OfficeId = officeId,
            Period = period,
            Flags = copy,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = concurrencyToken,
        };
    }
}
