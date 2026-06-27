using System.Globalization;
using Avalonia.Data.Converters;
using Tsumugi.Domain.Enums;

namespace Tsumugi.App.Converters;

/// <summary>
/// プロジェクト内 enum を日本語ラベルへ変換するコンバータ。
/// ComboBox.ItemTemplate と DataGridTextColumn.Binding の双方で使う。
/// 文字列表記は MHLW 公式様式・障害者総合支援法の用語に合わせる。
/// </summary>
public sealed class EnumJapaneseLabelConverter : IValueConverter
{
    public static readonly EnumJapaneseLabelConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            DisabilityCertificateType t => t switch
            {
                DisabilityCertificateType.Physical => "身体障害者手帳",
                DisabilityCertificateType.Intellectual => "療育手帳",
                DisabilityCertificateType.Mental => "精神障害者保健福祉手帳",
                _ => t.ToString(),
            },
            Gender g => g switch
            {
                Gender.Unspecified => "（未設定）",
                Gender.Male => "男",
                Gender.Female => "女",
                Gender.Other => "その他",
                _ => g.ToString(),
            },
            BenefitType b => b switch
            {
                BenefitType.Care => "介護給付",
                BenefitType.Training => "訓練等給付",
                BenefitType.ChildSupport => "障害児通所支援",
                _ => b.ToString(),
            },
            SupportCategory s => s switch
            {
                SupportCategory.None => "区分なし",
                SupportCategory.Category1 => "区分1",
                SupportCategory.Category2 => "区分2",
                SupportCategory.Category3 => "区分3",
                SupportCategory.Category4 => "区分4",
                SupportCategory.Category5 => "区分5",
                SupportCategory.Category6 => "区分6",
                _ => s.ToString(),
            },
            PaymentBurdenCategory p => p switch
            {
                PaymentBurdenCategory.Unspecified => "（未設定）",
                PaymentBurdenCategory.Welfare => "生活保護",
                PaymentBurdenCategory.LowIncome => "低所得",
                PaymentBurdenCategory.General1 => "一般1",
                PaymentBurdenCategory.General2 => "一般2",
                _ => p.ToString(),
            },
            null => string.Empty,
            _ => value.ToString() ?? string.Empty,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
