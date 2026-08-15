namespace Tsumugi.Application.Abstractions;

/// <summary>
/// Application 層から見た DB の保存先。Infrastructure の ISqliteLocation とは別に宣言する
/// （依存方向: Application は Infrastructure を知らない）。実装は SqliteLocationService。
/// </summary>
public interface IDatabaseFileLocation
{
    /// <summary>DB ファイルの絶対パス。</summary>
    string DatabasePath { get; }

    /// <summary>自動バックアップと復元前退避の保存先ディレクトリの絶対パス。</summary>
    string BackupDirectory { get; }
}
