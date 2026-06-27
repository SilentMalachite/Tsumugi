using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Tsumugi.Infrastructure.Tests;

/// <summary>
/// アセンブリ自身の TypeReference / MemberReference のみを抽出する。
/// 推移閉包は意図的にたどらない（Avalonia 内部を巻き込まないため）。
/// </summary>
internal static class AssemblyMetadataScanner
{
    /// <summary>
    /// dllPath が直接参照している型の完全名（Namespace.Name）を重複排除して返す。
    /// TypeReference と「Parent が TypeReference の MemberReference」の両方を見る。
    /// </summary>
    public static IReadOnlyList<string> ScanReferencedTypeFullNames(string dllPath)
    {
        EnsureExists(dllPath);

        using var stream = File.OpenRead(dllPath);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();

        var results = new HashSet<string>(StringComparer.Ordinal);

        foreach (var handle in md.TypeReferences)
        {
            var t = md.GetTypeReference(handle);
            var ns = md.GetString(t.Namespace);
            var name = md.GetString(t.Name);
            results.Add(string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}");
        }

        foreach (var handle in md.MemberReferences)
        {
            var m = md.GetMemberReference(handle);
            if (m.Parent.Kind != HandleKind.TypeReference) continue;
            var t = md.GetTypeReference((TypeReferenceHandle)m.Parent);
            var ns = md.GetString(t.Namespace);
            var name = md.GetString(t.Name);
            results.Add(string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}");
        }

        var list = new List<string>(results);
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    /// <summary>
    /// dllPath が静的に宣言している P/Invoke (DllImport) を全件返す。
    /// 通信ライブラリ (ws2_32 / wininet / libcurl 等) への P/Invoke 混入を捕まえるための補助。
    /// </summary>
    public static IReadOnlyList<PInvokeImport> ScanPInvokeImports(string dllPath)
    {
        EnsureExists(dllPath);
        using var stream = File.OpenRead(dllPath);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();

        var hits = new List<PInvokeImport>();
        foreach (var handle in md.MethodDefinitions)
        {
            var def = md.GetMethodDefinition(handle);
            if (!def.Attributes.HasFlag(MethodAttributes.PinvokeImpl)) continue;
            var import = def.GetImport();
            if (import.Module.IsNil) continue;
            var module = md.GetModuleReference(import.Module);
            hits.Add(new PInvokeImport(md.GetString(module.Name), md.GetString(def.Name)));
        }
        hits.Sort(static (a, b) =>
        {
            var c = StringComparer.Ordinal.Compare(a.DllName, b.DllName);
            return c != 0 ? c : StringComparer.Ordinal.Compare(a.MethodName, b.MethodName);
        });
        return hits;
    }

    /// <summary>
    /// dll の生バイト列に対し、UTF-16 LE エンコードした needle が含まれるかを検査する。
    /// UserString ヒープ反復は MetadataReader の公開 API では難しいため、ヒープ実体である
    /// バイト列を直接走査するこの実装で代替する（ASCII の URL/語彙には十分実用的）。
    /// </summary>
    public static bool ContainsRawUtf16Substring(string dllPath, string needle)
    {
        EnsureExists(dllPath);
        ArgumentException.ThrowIfNullOrEmpty(needle);
        var encoded = Encoding.Unicode.GetBytes(needle);
        var allBytes = File.ReadAllBytes(dllPath);
        return IndexOf(allBytes, encoded) >= 0;
    }

    /// <summary>
    /// 大小文字を区別せずに UTF-16 LE 部分文字列を検索する。ASCII 文字に対してのみ
    /// 大小無視（low byte の ASCII 範囲のみ）。混在ケース（"Https://" 等）の取りこぼしを防ぐ。
    /// </summary>
    public static bool ContainsRawUtf16SubstringIgnoreCase(string dllPath, string needle)
    {
        EnsureExists(dllPath);
        ArgumentException.ThrowIfNullOrEmpty(needle);
        var lowered = Encoding.Unicode.GetBytes(needle.ToLowerInvariant());
        var bytes = File.ReadAllBytes(dllPath);
        return IndexOfAsciiCaseInsensitiveUtf16(bytes, lowered) >= 0;
    }

    public readonly record struct PInvokeImport(string DllName, string MethodName);

    private static int IndexOf(byte[] hay, byte[] needle)
    {
        if (needle.Length == 0 || hay.Length < needle.Length) return -1;
        var limit = hay.Length - needle.Length;
        for (var i = 0; i <= limit; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (hay[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }

    // UTF-16 LE 想定: 偶数 index は ASCII low byte、奇数 index は high byte(=0)。
    // needle は事前に ToLowerInvariant した小文字 ASCII 前提。
    private static int IndexOfAsciiCaseInsensitiveUtf16(byte[] hay, byte[] needleLower)
    {
        if (needleLower.Length == 0 || hay.Length < needleLower.Length) return -1;
        var limit = hay.Length - needleLower.Length;
        for (var i = 0; i <= limit; i++)
        {
            var match = true;
            for (var j = 0; j < needleLower.Length; j++)
            {
                var h = hay[i + j];
                var n = needleLower[j];
                if ((j & 1) == 0)
                {
                    // low byte: ASCII case-insensitive
                    var hl = (h >= (byte)'A' && h <= (byte)'Z') ? (byte)(h + 32) : h;
                    if (hl != n) { match = false; break; }
                }
                else
                {
                    if (h != n) { match = false; break; }
                }
            }
            if (match) return i;
        }
        return -1;
    }

    private static void EnsureExists(string dllPath)
    {
        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException(
                $"対象アセンブリが見つからない: {Path.GetFileName(dllPath)}. " +
                "事前に `dotnet build` 済みであることを確認。",
                dllPath);
        }
    }
}
