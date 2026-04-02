using System;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;
using Microsoft.Win32;

namespace EnterpriseWorkReport.Views.Pages
{
    public partial class SettingsPage : Page
    {
        private readonly LanServerService _lanServer;
        private string _logoPath = null;

        public SettingsPage()
        {
            InitializeComponent();
            
            // Restrict admin-only features for non-admin users
            if (!SessionManager.IsAdmin)
            {
                // Hide admin-only sections
                if (FindName("AuditLogSection") is UIElement auditSection)
                    auditSection.Visibility = Visibility.Collapsed;
                if (FindName("CompanySettingsSection") is UIElement companySection)
                    companySection.Visibility = Visibility.Collapsed;
                if (FindName("LanServerSection") is UIElement lanSection)
                    lanSection.Visibility = Visibility.Collapsed;
            }
            
            LoadAuditLog();
            LoadCompanySettings();
            
            // Initialize LAN server (admin only)
            if (SessionManager.IsAdmin)
            {
                _lanServer = new LanServerService(5050);
                _lanServer.LogMessage += OnLanServerLog;
            }

            // Set current theme radio button
            switch (ThemeService.CurrentTheme)
            {
                case AppTheme.Dark: ThemeNavyRadio.IsChecked = true; break;
                case AppTheme.Amoled: ThemeAmoledRadio.IsChecked = true; break;
                default: ThemeLightRadio.IsChecked = true; break;
            }
        }

        private void LoadCompanySettings()
        {
            var companyService = new CompanyService();
            var settings = companyService.GetSettings();
            if (settings != null)
            {
                CompanyNameBox.Text = settings.CompanyName ?? "";
                CompanyEmailBox.Text = settings.CompanyEmail ?? "";
                CompanyPhoneBox.Text = settings.CompanyPhone ?? "";
                CompanyAddressBox.Text = settings.CompanyAddress ?? "";
                TaxIdBox.Text = settings.TaxId ?? "";
                CurrencyBox.Text = settings.CurrencySymbol ?? "₹";
                
                _logoPath = settings.LogoPath;
                if (!string.IsNullOrWhiteSpace(_logoPath) && File.Exists(_logoPath))
                {
                    try
                    {
                        var bmp = new System.Windows.Media.Imaging.BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bmp.UriSource = new Uri(_logoPath, UriKind.Absolute);
                        bmp.EndInit();
                        SettingsLogoImage.Source = bmp;
                    }
                    catch { }
                }
            }
        }

        private void SaveCompanySettings_Click(object sender, RoutedEventArgs e)
        {
            // Security check: Only admins can save company settings
            if (!SessionManager.IsAdmin)
            {
                MessageBox.Show("Access Denied. Only administrators can modify company settings.", 
                    "Unauthorized", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            try
            {
                var companyService = new CompanyService();
                var settings = new CompanySettings
                {
                    CompanyName = CompanyNameBox.Text.Trim(),
                    CompanyEmail = CompanyEmailBox.Text.Trim(),
                    CompanyPhone = CompanyPhoneBox.Text.Trim(),
                    CompanyAddress = CompanyAddressBox.Text.Trim(),
                    TaxId = TaxIdBox.Text.Trim(),
                    CurrencySymbol = CurrencyBox.Text.Trim(),
                    LogoPath = _logoPath
                };
                companyService.UpdateSettings(settings);
                
                // Refresh shell window if it's currently open
                if (Window.GetWindow(this) is MainShellWindow shell)
                {
                    shell.LoadCompanyInfo();
                }

                CompanyStatusText.Text = "✓ Company settings saved successfully!";
                CompanyStatusText.Foreground = System.Windows.Media.Brushes.Green;
            }
            catch (Exception ex)
            {
                CompanyStatusText.Text = $"✕ Error: {ex.Message}";
                CompanyStatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void UploadLogo_Click(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.IsAdmin) return;
            
            var dlg = new OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp",
                Title = "Select Company Logo"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string uploadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EnterpriseWorkReport", "Uploads", "Images");
                    Directory.CreateDirectory(uploadsFolder);
                    
                    string ext = Path.GetExtension(dlg.FileName);
                    string targetPath = Path.Combine(uploadsFolder, $"company_logo_{DateTime.Now.Ticks}{ext}");
                    File.Copy(dlg.FileName, targetPath, true);
                    
                    _logoPath = targetPath;
                    
                    // Show in preview
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(targetPath, UriKind.Absolute);
                    bmp.EndInit();
                    SettingsLogoImage.Source = bmp;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to upload logo: {ex.Message}", "Error");
                }
            }
        }

        private void OnLanServerLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                ServerStatusText.Text = message;
                if (_lanServer.IsRunning)
                {
                    UpdateServerUI(true);
                }
            });
        }

        private void UpdateServerUI(bool isRunning)
        {
            if (isRunning)
            {
                ServerStatusText.Text = "✓ Server is running";
                ServerStatusText.Foreground = System.Windows.Media.Brushes.Green;
                StartServerBtn.Visibility = Visibility.Collapsed;
                StopServerBtn.Visibility = Visibility.Visible;
                ServerUrlsPanel.Visibility = Visibility.Visible;
                
                // Show access URLs
                var urls = GetAccessUrls();
                ServerUrlsText.Text = string.Join("\n", urls);
            }
            else
            {
                ServerStatusText.Text = "Server is NOT running";
                ServerStatusText.Foreground = System.Windows.Media.Brushes.Gray;
                StartServerBtn.Visibility = Visibility.Visible;
                StopServerBtn.Visibility = Visibility.Collapsed;
                ServerUrlsPanel.Visibility = Visibility.Collapsed;
            }
        }

        private string[] GetAccessUrls()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var urls = new System.Collections.Generic.List<string>();
                
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        urls.Add($"http://{ip}:5050");
                    }
                }
                
                urls.Insert(0, "http://localhost:5050");
                return urls.ToArray();
            }
            catch
            {
                return new[] { "http://localhost:5050" };
            }
        }

        private void LoadAuditLog()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                var logs = conn.Query<AuditLog>("SELECT * FROM AuditLogs ORDER BY Timestamp DESC LIMIT 200");
                AuditGrid.ItemsSource = logs;
            }
        }

        private void RefreshAuditLog_Click(object sender, RoutedEventArgs e) => LoadAuditLog();

        private void StartServer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _lanServer.Start();
                UpdateServerUI(true);
                AuditService.Log("LAN Server", "Network server started");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start server: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopServer_Click(object sender, RoutedEventArgs e)
        {
            _lanServer.Stop();
            UpdateServerUI(false);
            AuditService.Log("LAN Server", "Network server stopped");
        }

        private void BackupNow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupPath = Path.Combine(DatabaseService.BackupsFolder, $"backup_{timestamp}.db");
                File.Copy(DatabaseService.DbPath, backupPath, overwrite: true);
                BackupStatusText.Text = $"✔ Backup created: backup_{timestamp}.db";
                BackupStatusText.Foreground = System.Windows.Media.Brushes.Green;

                // Log the action
                AuditService.Log("Backup Created", $"Backup saved to {backupPath}");
                LoadAuditLog();
            }
            catch (Exception ex)
            {
                BackupStatusText.Text = $"✕ Backup failed: {ex.Message}";
                BackupStatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Backup File",
                Filter = "SQLite Database|*.db",
                InitialDirectory = DatabaseService.BackupsFolder
            };
            if (dlg.ShowDialog() != true) return;

            var confirm = MessageBox.Show(
                "WARNING: Restoring a backup will replace ALL current data.\n\nAre you sure?",
                "Confirm Restore", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                File.Copy(dlg.FileName, DatabaseService.DbPath, overwrite: true);
                BackupStatusText.Text = $"✔ Database restored from: {Path.GetFileName(dlg.FileName)}. Please restart the application.";
                BackupStatusText.Foreground = System.Windows.Media.Brushes.Green;
            }
            catch (Exception ex)
            {
                BackupStatusText.Text = $"✕ Restore failed: {ex.Message}";
                BackupStatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }
        private void Theme_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return; // Prevent triggering during initialization
            
            if (sender == ThemeLightRadio) ThemeService.SetTheme(AppTheme.Light);
            else if (sender == ThemeNavyRadio) ThemeService.SetTheme(AppTheme.Dark);
            else if (sender == ThemeAmoledRadio) ThemeService.SetTheme(AppTheme.Amoled);
        }

        private async void LoadDemo_Click(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.IsAdmin) return;

            var confirm = MessageBox.Show(
                "CRITICAL WARNING: Loading demo data will PERMANENTLY DELETE all current projects, users, and reports.\n\n" +
                "Are you absolutely sure you want to proceed with a clean reset?",
                "Dangerous Operation", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                LoadDemoBtn.IsEnabled = false;
                LoadDemoBtn.Content = "⏳ Generating Data...";
                
                await System.Threading.Tasks.Task.Run(() =>
                {
                    TestDataGenerator.GenerateAllTestData();
                });

                MessageBox.Show("Demo data generated successfully! The system has been reset with a full set of professional sample records.", 
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Refresh audit log to show recent activity
                LoadAuditLog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to generate demo data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadDemoBtn.IsEnabled = true;
                LoadDemoBtn.Content = "🚀 Load Demo Data";
            }
        }
    }
}
