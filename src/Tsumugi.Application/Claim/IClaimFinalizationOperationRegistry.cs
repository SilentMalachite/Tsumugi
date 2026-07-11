using System.Security.Cryptography;
using Tsumugi.Application.Abstractions;

namespace Tsumugi.Application.Claim;

public interface IClaimFinalizationOperation
{
    string SchemaVersion { get; }
    ClaimFinalizationOperationPayload Canonicalize(ClaimFinalizationDraft draft);
    ClaimFinalizationOperationPayload Rebuild(
        ClaimBatchAggregate aggregate,
        IReadOnlyList<ClaimFinalizationDetailDraft> details);
}

public sealed class ClaimFinalizationOperationPayload
{
    private readonly byte[] _canonicalUtf8;

    public ClaimFinalizationOperationPayload(ReadOnlySpan<byte> canonicalUtf8)
    {
        _canonicalUtf8 = canonicalUtf8.ToArray();
        Sha256 = Convert.ToHexStringLower(SHA256.HashData(_canonicalUtf8));
    }

    public string Sha256 { get; }
    public byte[] GetCanonicalUtf8Bytes() => [.. _canonicalUtf8];
}

public sealed record ClaimFinalizationOperationEntry(
    IClaimFinalizationOperation Operation,
    bool CanWrite);

public interface IClaimFinalizationOperationRegistry
{
    ClaimFinalizationOperationEntry? GetWriteEntry(string schemaVersion);
    ClaimFinalizationOperationEntry? GetReadEntry(string schemaVersion);
}
