namespace Tsumugi.Infrastructure.Csv.Specifications;

/// <summary>
/// CSV 仕様の各判断に対する行単位の出典（証跡台帳）。制度マスタ側（<c>ClaimMasters/Seed/*.json</c>）が
/// 行単位に持っている <c>sourceRefs</c> と同型の形を CSV 仕様側にも与える。
/// </summary>
/// <remarks>
/// <para>
/// 目的は「文書が変わったときに、<b>どの判断の根拠を再検証すべきか</b>を機械が名指しできる」こと。
/// <c>sources.json</c> の <c>liveCheck</c> は「文書が変わった」までしか分からない。各 ref が
/// 文書 ID と SHA-256 を持ち、<see cref="CsvSpecificationLoader"/> が登録値との一致を検証するため、
/// 文書を差し替えて <c>sources.json</c> を更新すると、その文書に依拠する claim が fail-close する。
/// </para>
/// <para>
/// 項目表そのもの（項目名・桁数・属性）の裏付けは機械抽出＋突合（ADR 0037）が担う。この台帳は
/// <b>本文（規則）由来の判断</b>と<b>他文書（留意事項通知等）に依拠する判断</b>を対象にする。
/// </para>
/// </remarks>
/// <param name="ClaimId">
/// 判断の対象。<c>rule:*</c>（横断規則）／fieldId／recordId のいずれか。
/// </param>
/// <param name="ClaimKind">rule / field / record のいずれか。</param>
/// <param name="Decision">その出典から何を決めたか（実装のどこに現れるかを含めて 1〜2 行）。</param>
public sealed record CsvSpecEvidenceClaim(
    string ClaimId,
    string ClaimKind,
    string Decision,
    IReadOnlyList<CsvSpecSourceRef> SourceRefs);

/// <param name="Locator">
/// 文書内位置。<c>p.63;item=52</c> のように <c>;</c> 区切りの <c>key=value</c> と <c>p.N</c> で表す。
/// <c>p.N;item=M</c> 形式は機械抽出の結果と突合できる（<c>ProviderEvidenceLocatorTests</c>）。
/// </param>
/// <param name="EvidenceRole">authoritative（その判断の根拠）／cross-check（裏取り）。</param>
/// <param name="Supports">その出典が何を裏付けるか（閉じた語彙）。</param>
/// <param name="Quote">原文引用。文書が変わったときに「まだ同じことを言っているか」を見るために置く。</param>
public sealed record CsvSpecSourceRef(
    string DocumentId,
    string Sha256,
    string Locator,
    string EvidenceRole,
    IReadOnlyList<string> Supports,
    string? Quote = null);

/// <param name="ClaimId">出典が未付与の対象（個別 ID またはパターン表記）。</param>
/// <param name="Reason">なぜ未付与か。</param>
/// <param name="TrackedIn">どこで追跡しているか。</param>
public sealed record CsvSpecEvidenceGap(string ClaimId, string Reason, string TrackedIn);
