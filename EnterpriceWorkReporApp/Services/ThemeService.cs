using System;
using System.Linq;
using System.Windows;

namespace EnterpriseWorkReport.Services
{
    public enum AppTheme
    {
        Light,
        Dark,
        Amoled
    }

    public static class ThemeService
    {
        private static AppTheme _currentTheme = AppTheme.Light;
        public static AppTheme CurrentTheme => _currentTheme;

        public static void SetTheme(AppTheme theme)
        {
            _currentTheme = theme;
            var app = (App)Application.Current;
            
            var mergedDicts = Application.Current.Resources.MergedDictionaries;

            // Ensure baseline color definitions are present.
            if (!mergedDicts.Any(d => d.Source != null && d.Source.OriginalString.Contains("Colors.xaml")))
            {
                mergedDicts.Insert(0, new ResourceDictionary { Source = new Uri("Themes/Colors.xaml", UriKind.Relative) });
            }

            // Remove existing theme dictionary (Light/Dark/Amoled) if present.
            var existingThemeDict = mergedDicts.FirstOrDefault(d => 
                d.Source != null && (
                    d.Source.OriginalString.Contains("LightTheme.xaml") || 
                    d.Source.OriginalString.Contains("DarkColors.xaml") || 
                    d.Source.OriginalString.Contains("AmoledColors.xaml")
                ));

            if (existingThemeDict != null)
            {
                mergedDicts.Remove(existingThemeDict);
            }

            // Create and add new theme dictionary
            string themeUri = theme switch
            {
                AppTheme.Dark => "Themes/DarkColors.xaml",
                AppTheme.Amoled => "Themes/AmoledColors.xaml",
                _ => "Themes/LightTheme.xaml"
            };

            var newThemeDict = new ResourceDictionary
            {
                Source = new Uri(themeUri, UriKind.Relative)
            };

            // Insert at the beginning to ensure it's overridden by Styles.xaml if needed, 
            // but actually Styles.xaml should come AFTER colors.
            mergedDicts.Insert(0, newThemeDict);
            
            // Save preference (optional, could be in CompanySettings or local file)
            // For now, we just keep it in memory for the session
        }
    }
}
