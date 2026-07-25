using CommunityToolkit.Mvvm.ComponentModel;
using Tsumugi.Application.Abstractions;

namespace Tsumugi.App.ViewModels.Claim;

/// <summary>
/// 汎用 pass-through 入力（ADR 0042）の 1 欄。ラベル・補助文・型・桁数は CSV 仕様側の宣言
/// （<see cref="ClaimGenericFieldDeclaration"/>）から取り、UI にハードコードしない。
/// </summary>
public sealed partial class ClaimGenericInputField : ObservableObject
{
    public ClaimGenericInputField(ClaimGenericFieldDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        Declaration = declaration;
    }

    public ClaimGenericFieldDeclaration Declaration { get; }

    public string Name => Declaration.Name;

    public string Label => Declaration.Label;

    public string Help => Declaration.Help;

    /// <summary>入力値（文字列）。型・桁数の検証は保存時に仕様側の宣言で行う。</summary>
    [ObservableProperty] private string? _value;
}
