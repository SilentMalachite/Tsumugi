namespace Tsumugi.Infrastructure.Persistence;

public interface ISqliteLocation
{
    string DatabasePath { get; }
    string ConnectionString { get; }

    /// <summary>
    /// 保存先ディレクトリと DB ファイルを作成し、OS 別の最小権限を強制する。
    /// 冪等。複数回呼ばれても安全。失敗時は説明的な例外を投げる（パスはログに残さない）。
    /// </summary>
    void EnsureSecuredStorage();
}
