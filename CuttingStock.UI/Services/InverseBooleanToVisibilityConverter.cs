using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CuttingStock.UI.Services
{
    /// <summary>
    /// Inverse of <see cref="System.Windows.Controls.BooleanToVisibilityConverter"/>.
    /// `true` → <see cref="Visibility.Collapsed"/>, `false` → <see cref="Visibility.Visible"/>.
    ///
    /// The built-in `BooleanToVisibilityConverter` does NOT honor `ConverterParameter`,
    /// so for "show placeholder when X is false" bindings we need this dedicated converter.
    /// </summary>
    public sealed class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool b = value is bool x && x;
            return b ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is Visibility v && v == Visibility.Collapsed;
    }
}
