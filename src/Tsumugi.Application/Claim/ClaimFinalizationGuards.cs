using Tsumugi.Application.Abstractions;

namespace Tsumugi.Application.Claim;

/// <summary>
/// 確定請求の書込み・読出し双方で使う共通ガード。文字列形式の下限規則と codec 呼び出しの
/// 例外正規化をここに一元化し、write 経路（<c>ClaimFinalizationStore</c>）と read 経路
/// （<see cref="ClaimHistoryVerifier"/>）が別々の規則を持たないようにする。
/// </summary>
public static class ClaimFinalizationGuards
{
    /// <summary>非空かつ 64 文字以内（永続化列の上限と監査可能性の下限）。</summary>
    public static bool Bounded(string value)
        => !string.IsNullOrWhiteSpace(value) && value.Length <= 64;

    /// <summary>ASCII のみの <see cref="Bounded"/>（版識別子・スキーマ名に用いる）。</summary>
    public static bool AsciiBounded(string value)
        => Bounded(value) && value.All(character => character <= 0x7f);

    /// <summary>小文字16進 64 桁の SHA-256 文字列か。</summary>
    public static bool LowerSha256(string value)
        => value is { Length: 64 }
            && value.All(character =>
                character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    /// <summary>
    /// codec 呼び出しの例外を <see cref="ClaimErrorCode.InvalidSnapshotEnvelope"/> に正規化する。
    /// codec は差し替え可能なので、どんな例外型で失敗しても「envelope が信用できない」に畳む。
    /// </summary>
    public static void InvokeCodec(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        InvokeCodec(() =>
        {
            action();
            return true;
        });
    }

    /// <inheritdoc cref="InvokeCodec(Action)"/>
    public static T InvokeCodec<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            return action();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new ClaimFinalizationException(ClaimErrorCode.InvalidSnapshotEnvelope);
        }
    }
}
