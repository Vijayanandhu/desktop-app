using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport
{
    public partial class App : Application
    {
        private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_data", "error.log");

        protected override void OnStartup(StartupEventArgs e)
        {
            // Set up global exception handlers to prevent unexpected crashes
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            base.OnStartup(e);

            if (e.Args.Length > 0 && e.Args[0] == "--test")
            {
                EnterpriseWorkReport.Services.TestSuite.RunAll();
                return;
            }

            if (e.Args.Length > 0 && e.Args[0] == "--testdata")
            {
                DatabaseService.InitializeDatabase();
                EnterpriseWorkReport.Services.TestDataGenerator.GenerateAllTestData();
                MessageBox.Show("Test data generation complete! You can now login with:\n\nAdmin: admin/admin123\nUsers: user1-user15/password123", "Test Data Generated", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Initialize database with multi-user support
            DatabaseService.InitializeDatabase();

            // Ensure theme resource dictionary is loaded during startup (addresses StaticResource failures for brushes)
            ThemeService.SetTheme(AppTheme.Light);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception, "Dispatcher");
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nThe application will continue running.",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogException(ex, "AppDomain");
            }
        }

        private void LogException(Exception ex, string source)
        {
            try
            {
                var logDir = Path.GetDirectoryName(LogPath);
                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);

                var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n";
                File.AppendAllText(LogPath, logMessage);
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }
}
