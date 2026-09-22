using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Qylent.Kutuphane.Core.Domain;

namespace Qylent.Kutuphane.App.Ui;

public interface IThemeService
{
    ThemePreference Preference { get; }
    void Apply(ThemePreference preference);
}

public sealed class ThemeService : IThemeService, IDisposable
{
    private static readonly IReadOnlyDictionary<string, string> LightPalette = new Dictionary<string, string>
    {
        ["Paper"] = "#F7F9FC", ["Surface"] = "#FCFDFF", ["Ink"] = "#192235", ["Muted"] = "#566176",
        ["Rule"] = "#D9E0EC", ["Accent"] = "#2457D6", ["AccentDark"] = "#173FAD", ["AccentInk"] = "#F9FBFF",
        ["Hover"] = "#EFF3FA", ["Pressed"] = "#E5EBF6", ["Header"] = "#EEF2F8", ["Error"] = "#B42318",
        ["Success"] = "#18794E", ["Focus"] = "#0B6EF3"
    };

    private static readonly IReadOnlyDictionary<string, string> DarkPalette = new Dictionary<string, string>
    {
        ["Paper"] = "#101522", ["Surface"] = "#171E2E", ["Ink"] = "#F2F5FA", ["Muted"] = "#B7C0D1",
        ["Rule"] = "#354057", ["Accent"] = "#7DA2FF", ["AccentDark"] = "#AFC3FF", ["AccentInk"] = "#091225",
        ["Hover"] = "#222C40", ["Pressed"] = "#2C3850", ["Header"] = "#20293B", ["Error"] = "#FF8A80",
        ["Success"] = "#62D49B", ["Focus"] = "#91B2FF"
    };

    public ThemePreference Preference { get; private set; } = ThemePreference.System;

    public ThemeService() => SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

    public void Apply(ThemePreference preference)
    {
        Preference = preference;
        var useDark = preference == ThemePreference.Dark || preference == ThemePreference.System && WindowsUsesDarkTheme();
        var palette = useDark ? DarkPalette : LightPalette;
        foreach (var (key, color) in palette)
        {
            Application.Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (Preference == ThemePreference.System)
            Application.Current.Dispatcher.Invoke(() => Apply(ThemePreference.System));
    }

    private static bool WindowsUsesDarkTheme()
    {
        try
        {
            var value = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
            return value is int number && number == 0;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
}
