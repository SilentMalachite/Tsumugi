namespace Tsumugi.Application;

/// <summary>
/// 編集対象を画面で読んだときの ConcurrencyToken と、現在 DB に格納されているトークンが一致しないときに投げる。
/// 別ユーザによる先行更新を検知するための Application 層例外（CLAUDE.md ハード制約: 楽観的同時実行）。
/// EF Core への依存を Application 層に持ち込まないため、独自例外として定義する。
/// </summary>
public sealed class OptimisticConcurrencyException : Exception
{
    public OptimisticConcurrencyException(string entityName, Guid id)
        : base($"{entityName} (Id={id}) は他で更新されています。最新の状態を再読込してください。")
    {
        EntityName = entityName;
        Id = id;
    }

    public string EntityName { get; }
    public Guid Id { get; }
}
