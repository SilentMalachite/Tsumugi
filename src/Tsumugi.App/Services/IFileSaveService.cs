using System.Threading;
using System.Threading.Tasks;

namespace Tsumugi.App.Services;

/// <summary>ファイル保存ダイアログを介して指定バイト列をユーザ指定パスへ書き出す抽象。</summary>
/// <remarks>VM 層からテスト可能にするための薄い抽象。UI 実装は <see cref="AvaloniaFileSaveService"/>。</remarks>
public interface IFileSaveService
{
    /// <summary>
    /// 保存ダイアログを開き、ユーザが選択したパスへ <paramref name="bytes"/> を書き出す。
    /// </summary>
    /// <param name="bytes">書き出すバイト列。</param>
    /// <param name="suggestedFileName">既定ファイル名（拡張子含めても可）。</param>
    /// <param name="fileTypeName">ダイアログのファイル種別名（例: "PDF"）。</param>
    /// <param name="extension">既定拡張子（例: ".pdf"、ドット必須）。</param>
    /// <param name="ct">キャンセル要求。</param>
    /// <returns>ユーザが保存先を確定し書き込みが完了したら <c>true</c>、キャンセル時は <c>false</c>。</returns>
    Task<bool> SaveAsync(byte[] bytes, string suggestedFileName, string fileTypeName, string extension, CancellationToken ct = default);
}
