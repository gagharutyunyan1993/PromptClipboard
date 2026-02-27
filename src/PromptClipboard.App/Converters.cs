namespace PromptClipboard.App;

using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

public static class Converters
{
    public static IValueConverter HasTemplateConverter { get; } = new HasTemplateVariablesConverter();
    public static IValueConverter JsonToTagListConverter { get; } = new JsonToTagListConverterImpl();
    public static IValueConverter TagsNotEmptyConverter { get; } = new TagsNotEmptyConverterImpl();
    public static IValueConverter BoolToPinColorConverter { get; } = new BoolToPinColorConverterImpl();
    public static IValueConverter StringEmptyToVisibleConverter { get; } = new StringEmptyToVisibleConverterImpl();
    public static IValueConverter InverseBoolToVisibilityConverter { get; } = new InverseBoolToVisibilityConverterImpl();
    public static IValueConverter BoolToVisibilityConverter { get; } = new BoolToVisibilityConverterImpl();

    private sealed class HasTemplateVariablesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is string s && s.Contains("{{");

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    private sealed class JsonToTagListConverterImpl : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string json && !string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                }
                catch { }
            }
            return new List<string>();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    private sealed class TagsNotEmptyConverterImpl : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string json && !string.IsNullOrWhiteSpace(json) && json != "[]")
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    private sealed class BoolToPinColorConverterImpl : IValueConverter
    {
        private static readonly SolidColorBrush PinnedBrush = new(Color.FromRgb(0xfb, 0xbf, 0x24));
        private static readonly SolidColorBrush UnpinnedBrush = new(Color.FromRgb(0x94, 0xa3, 0xb8));

        static BoolToPinColorConverterImpl()
        {
            PinnedBrush.Freeze();
            UnpinnedBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? PinnedBrush : UnpinnedBrush;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    private sealed class StringEmptyToVisibleConverterImpl : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    private sealed class InverseBoolToVisibilityConverterImpl : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    private sealed class BoolToVisibilityConverterImpl : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
