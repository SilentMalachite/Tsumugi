namespace Tsumugi.Application.Claim;

/// <summary>
/// 請求確定draftへ記録するアプリ側の版メタデータ（制度値ではない）。
/// Phase 3-0はDB列と検証契約だけを確立しproduction版文字列を定義していないため、
/// 本スライスのmilestone名で固定する。CSV/帳票の版は、typed requirementの由来である
/// 埋め込みcatalog（`field-mapping-r7-10` / `report-field-mapping-r8-06`）を識別子として指す。
/// Phase 3-2（帳票）・Phase 3-3（CSV出力）の実装時に実仕様版へ置き換える。
/// </summary>
public static class ClaimFinalizationVersions
{
    public const string OperationApplicationVersion = "tsumugi-operation-phase3-1-slice";
    public const string SnapshotApplicationVersion = "tsumugi-snapshot-phase3-1-slice";
    public const string CsvSpecificationVersion = "field-mapping-r7-10";
    public const string ReportSpecificationVersion = "report-field-mapping-r8-06";
}
