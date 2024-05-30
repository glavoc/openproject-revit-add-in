﻿using System.Windows;
using Autodesk.Navisworks.Api.Plugins;
using MessageBox = System.Windows.MessageBox;

namespace OpenProjectNavisworks.Model;

public static class StaticUtil
{
    public static void ShowWarning(string msg)
    {
        MessageBox.Show(msg, DefaultSetting.AppName, MessageBoxButton.OK, MessageBoxImage.Exclamation);
    }
    public static string CommandFullName = typeof(AddInPlugin).FullName;
   
    /// <summary>
    /// CaseInsensitiveContains
    /// </summary>
    /// <param name="text"></param>
    /// <param name="value"></param>
    /// <param name="stringComparison"></param>
    /// <returns></returns>
    public static bool CaseInsensitiveContains(this string text, string value, StringComparison stringComparison = StringComparison.CurrentCultureIgnoreCase)
    {
        return text.IndexOf(value, stringComparison) >= 0;
    }
}