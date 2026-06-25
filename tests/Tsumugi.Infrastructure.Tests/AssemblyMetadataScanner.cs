using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

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
        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException(
                $"対象アセンブリが見つからない: {Path.GetFileName(dllPath)}. " +
                "事前に `dotnet build` 済みであることを確認。",
                dllPath);
        }

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
}
