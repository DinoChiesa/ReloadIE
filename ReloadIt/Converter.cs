using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Collections.Generic;

namespace ReloadIt.Converter
{
    public sealed class FlavorToChar : MarkupExtension, IValueConverter
    {
        private readonly static FlavorToChar _instance = new FlavorToChar();
        public static FlavorToChar Instance  { get { return _instance; } }
        static FlavorToChar() { /* required for lazy init */ }
        private FlavorToChar() { /* required to prevent public access */ }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return _instance;
        }

        public object Convert(object value,
                              Type targetType,
                              object parameter,
                              System.Globalization.CultureInfo culture)
        {
            if (value is int)
            {
                int v = (int)value;
                if (v == 0) return 'i';
                if (v == 1) return '!';
            }
            return '?';
        }

        public object ConvertBack(object value,
                                  Type targetType,
                                  object parameter,
                                  System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }

#if UNNECESSARY
    public sealed class VisibleWhenZero : MarkupExtension, IValueConverter
    {
        private readonly static VisibleWhenZero _instance = new VisibleWhenZero();
        public static VisibleWhenZero Instance  { get { return _instance; } }
        static VisibleWhenZero() { /* required for lazy init */ }
        private VisibleWhenZero() { /* required to prevent public access */ }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return _instance;
        }

        public object Convert(object value,
                              Type targetType,
                              object parameter,
                              System.Globalization.CultureInfo culture)
        {
            if (value is int)
            {
                int a = (int) value;
                return (a==0) ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }
        public object ConvertBack(object value,
                                  Type targetType,
                                  object parameter,
                                  System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }


    public sealed class  VisibleWhenNonZero : MarkupExtension, IValueConverter
    {
        private readonly static VisibleWhenNonZero _instance = new VisibleWhenNonZero();
        public static VisibleWhenNonZero Instance  { get { return _instance; } }
        static VisibleWhenNonZero() { /* required for lazy init */ }
        private VisibleWhenNonZero() { /* required to prevent public access */ }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return _instance;
        }

        public object Convert(object value,
                              Type targetType,
                              object parameter,
                              System.Globalization.CultureInfo culture)
        {
            if (value is int)
            {
                int a = (int)value;
                return (a!=0) ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }


        public object ConvertBack(object value,
                                  Type targetType,
                                  object parameter,
                                  System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }
#endif
}
