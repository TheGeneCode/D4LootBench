using System.Globalization;
using System.Windows.Data;
using ThunderEagle.FilterForge.App.ViewModels;

namespace ThunderEagle.FilterForge.App.Converters;

[ValueConversion(typeof(ConditionType), typeof(string))]
public sealed class ConditionTypeNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ConditionType ct
            ? ct switch
            {
                ConditionType.ItemPower       => "Item Power",
                ConditionType.Rarity          => "Rarity",
                ConditionType.ItemProperties  => "Item Properties",
                ConditionType.GreaterAffix    => "Greater Affix",
                ConditionType.Codex           => "Codex of Power",
                ConditionType.ItemType        => "Item Type",
                ConditionType.RequiredAffixes => "Required Affixes",
                ConditionType.OptionalAffixes => "Optional Affixes",
                ConditionType.SpecificUnique  => "Specific Unique",
                ConditionType.TalismanSet     => "Talisman Set",
                _                             => ct.ToString()
            }
            : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
