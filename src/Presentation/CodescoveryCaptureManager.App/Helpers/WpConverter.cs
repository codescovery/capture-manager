﻿using System;
using System.Globalization;
using System.Windows.Data;

namespace CodescoveryCaptureManager.App.Helpers
{
    internal class WpConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Int32.Parse(value.ToString()) - 200;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
