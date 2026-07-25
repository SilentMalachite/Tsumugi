using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>
/// 国保連請求CSVの出力履歴（追記型）。同一<see cref="ClaimBatchId"/>に対する再出力・訂正出力は
/// 既存行を書き換えず新しいレコードとして追記する。出力バイト列自体は保持せず、
/// 再現に必要な入力（<see cref="ProcessingMonth"/>）と版（<see cref="CsvSpecificationVersion"/> /
/// <see cref="ClaimMasterVersion"/>）と結果の指紋（<see cref="Sha256"/> / <see cref="ByteLength"/>）
/// だけを残す。氏名・受給者証番号・保存先パスは持たない（CLAUDE.md §ハード制約4）。
/// </summary>
public sealed record ClaimCsvExport : Entity
{
    public required Guid ClaimBatchId { get; init; }

    /// <summary>コントロールレコードへ書き込んだ処理対象年月。サービス提供年月とは独立した入力。</summary>
    public required ProcessingMonth ProcessingMonth { get; init; }

    public required string CsvSpecificationVersion { get; init; }
    public required string ClaimMasterVersion { get; init; }

    /// <summary>出力バイト列のSHA-256（64文字の小文字16進数）。</summary>
    public required string Sha256 { get; init; }

    /// <summary>出力バイト列の長さ。</summary>
    public required int ByteLength { get; init; }

    public static ClaimCsvExport NewRecord(
        Guid id,
        Guid claimBatchId,
        ProcessingMonth processingMonth,
        string csvSpecificationVersion,
        string claimMasterVersion,
        string sha256,
        int byteLength,
        string createdBy,
        DateTimeOffset createdAt)
    {
        RequireIdentity(id, nameof(id));
        RequireIdentity(claimBatchId, nameof(claimBatchId));
        _ = processingMonth.ToInt();
        ArgumentException.ThrowIfNullOrWhiteSpace(csvSpecificationVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(claimMasterVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteLength);
        RequireLowercaseSha256(sha256, nameof(sha256));

        return new ClaimCsvExport
        {
            Id = id,
            ClaimBatchId = claimBatchId,
            ProcessingMonth = processingMonth,
            CsvSpecificationVersion = csvSpecificationVersion,
            ClaimMasterVersion = claimMasterVersion,
            Sha256 = sha256,
            ByteLength = byteLength,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,
        };
    }

    private static void RequireLowercaseSha256(string value, string parameterName)
    {
        if (value is not { Length: 64 }
            || value.Any(character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "Sha256 は64文字の小文字16進数でなければなりません。", parameterName);
        }
    }

    private static void RequireIdentity(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("IDが空です。", parameterName);
    }
}
