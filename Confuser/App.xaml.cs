using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Controls;
using System.Globalization;

namespace Confuser
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
        static App()
        {
            //DARKNESS!!!
            SolidColorBrush current = SystemColors.HighlightBrush;
            FieldInfo colorCacheField = typeof(SystemColors).GetField("_colorCache",
                BindingFlags.Static | BindingFlags.NonPublic);
            Color[] _colorCache = (Color[])colorCacheField.GetValue(typeof(SystemColors));
            _colorCache[14] = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
        }
    }
    public class LeftMarginMultiplierConverter : IValueConverter
    {
        public double Length { get; set; }

        static int GetDepth(TreeViewItem item)
        {
            FrameworkElement elem = item;
            while (VisualTreeHelper.GetParent(elem) != null)
            {
                var tvi = VisualTreeHelper.GetParent(elem) as TreeViewItem;
                if (tvi != null)
                    return GetDepth(tvi) + 1;
                elem = VisualTreeHelper.GetParent(elem) as FrameworkElement;
            }
            return 0;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var item = value as TreeViewItem;
            if (item == null)
                return new Thickness(0);

            return new Thickness(Length * GetDepth(item), 0, 0, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }
}