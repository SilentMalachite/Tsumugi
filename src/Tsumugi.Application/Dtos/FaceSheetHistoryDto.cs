namespace Tsumugi.Application.Dtos;

/// <summary>フェースシート業務フィールドの1件分の差分。表示文言は UI が組み立てる。</summary>
public sealed record FaceSheetChangeDto(string PropertyName, string? OldValue, string? NewValue);

/// <summary>
/// フェースシート履歴の1版。版メタデータは既存 <see cref="FaceSheetDto"/> を再利用し、
/// 直前版からの差分を持つ。最古版の差分は空。
/// </summary>
public sealed record FaceSheetHistoryDto(
    FaceSheetDto FaceSheet,
    IReadOnlyList<FaceSheetChangeDto> ChangesFromPrevious);
