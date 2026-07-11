using System.Collections.Frozen;

namespace Tsumugi.Application.Claim;

public sealed class ClaimFinalizationOperationRegistry : IClaimFinalizationOperationRegistry
{
    private readonly FrozenDictionary<string, ClaimFinalizationOperationEntry> _entries;

    public ClaimFinalizationOperationRegistry()
        : this([new ClaimFinalizationOperationEntry(new ClaimFinalizationOperationV1(), CanWrite: true)])
    {
    }

    public ClaimFinalizationOperationRegistry(IEnumerable<ClaimFinalizationOperationEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = entries.ToFrozenDictionary(
            entry => entry.Operation.SchemaVersion,
            StringComparer.Ordinal);
    }

    public ClaimFinalizationOperationEntry? GetWriteEntry(string schemaVersion)
        => _entries.TryGetValue(schemaVersion, out var entry) && entry.CanWrite ? entry : null;

    public ClaimFinalizationOperationEntry? GetReadEntry(string schemaVersion)
        => _entries.GetValueOrDefault(schemaVersion);
}
