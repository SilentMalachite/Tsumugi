namespace Tsumugi.Application.Abstractions;

/// <summary>
/// バックアップディレクトリの列挙と削除。Application がファイルシステムを直接触らないための抽象。
/// </summary>
public interface IBackupDirectory
{
    /// <summary>ディレクトリ直下のファイル名（パスを含まない）を返す。</summary>
    IReadOnlyList<string> ListFileNames();

    /// <summary>ディレクトリ直下の指定ファイルを削除する。存在しない場合は何もしない。</summary>
    void Delete(string fileName);
}
