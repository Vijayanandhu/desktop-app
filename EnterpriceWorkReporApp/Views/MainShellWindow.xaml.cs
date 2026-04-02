using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using EnterpriseWorkReport.Services;
using EnterpriseWorkReport.Views.Pages;
using EnterpriseWorkReport.Views.Dialogs;

namespace EnterpriseWorkReport.Views
{
    public partial class MainShellWindow : Window
    {
        private readonly DispatcherTimer _clockTimer;
        private Button _activeNavButton;
        private readonly Dictionary<string, Button> _navButtons;

        public MainShellWindow()
        {
            InitializeComponent();
            LoadCompanyInfo();
            LoadUserProfile();



            // Hide admin section for non-admins
            if (!SessionManager.IsAdmin)
            {
                AdminSection.Visibility = Visibility.Collapsed;
                NavBulkOps.Visibility = Visibility.Collapsed;
                NavUsers.Visibility = Visibility.Collapsed;
            }

            // Build nav button map
            _navButtons = new Dictionary<string, Button>
            {
                { "Dashboard", NavDashboard },
                { "Projects", NavProjects },
                { "WorkReports", NavWorkReports },
                { "Billing", NavBilling },
                { "Attendance", NavAttendance },
                { "Leave", NavLeave },
                { "Quality", NavQuality },
                { "Leaderboard", NavLeaderboard },
                { "Messages", NavMessages },
                { "BulkOps", NavBulkOps },
                { "Users", NavUsers },
                { "Settings", NavSettings }
            };

            _activeNavButton = NavDashboard;

            // Start clock
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => UpdateClock();
            _clockTimer.Start();
            UpdateClock();

            NavigateTo("Dashboard");
        }

        public void LoadCompanyInfo()
        {
            try
            {
                using (var conn = DatabaseService.GetConnection())
                {
                    var settings = Dapper.SqlMapper.QueryFirstOrDefault<EnterpriseWorkReport.Models.CompanySettings>(
                        conn, "SELECT * FROM CompanySettings WHERE Id = 1");
                    
                    if (settings != null)
                    {
                        CompanyNameText.Text = string.IsNullOrWhiteSpace(settings.CompanyName) ? "WorkReport" : settings.CompanyName;
                        
                        if (!string.IsNullOrWhiteSpace(settings.LogoPath) && System.IO.File.Exists(settings.LogoPath))
                        {
                            var uri = new Uri(settings.LogoPath, UriKind.Absolute);
                            CompanyLogoImage.Source = new System.Windows.Media.Imaging.BitmapImage(uri);
                            CompanyLogoImage.Visibility = Visibility.Visible;
                            CompanyLogoTextFallback.Visibility = Visibility.Collapsed;
                            CompanyLogoBorder.Background = System.Windows.Media.Brushes.Transparent;
                        }
                    }
                }
            }
            catch { /* Ignore database load issues silently or log */ }
        }

        public void LoadUserProfile()
        {
            var user = SessionManager.CurrentUser;
            if (user == null) return;

            UserFullNameText.Text = user.FullName ?? "User";
            UserRoleText.Text = user.Role ?? "Employee";
            UserInitialText.Text = (user.FullName?.Length > 0) ? user.FullName[0].ToString().ToUpper() : "U";

            if (!string.IsNullOrWhiteSpace(user.ProfilePicture) && System.IO.File.Exists(user.ProfilePicture))
            {
                try 
                {
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(user.ProfilePicture, UriKind.Absolute);
                    bmp.EndInit();
                    UserProfileImage.Source = bmp;
                    UserProfileImage.Visibility = Visibility.Visible;
                } 
                catch { }
            }
        }



        private void UpdateClock()
        {
            DateTimeText.Text = DateTime.Now.ToString("dddd, dd MMM yyyy   hh:mm tt");
            // Update maximize button icon based on window state
            MaximizeBtn.Content = WindowState == WindowState.Maximized ? "❐" : "☐";
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void HelpBtn_Click(object sender, RoutedEventArgs e)
        {
            var helpDialog = new HelpDialog { Owner = this };
            helpDialog.ShowDialog();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            // Update maximize button icon when window state changes
            MaximizeBtn.Content = WindowState == WindowState.Maximized ? "❐" : "☐";
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                string tag = btn.Tag?.ToString();
                NavigateTo(tag);
            }
        }

        private void NavigateTo(string page)
        {
            // Update nav button styles
            if (_activeNavButton != null)
                _activeNavButton.Style = (Style)FindResource("SidebarNavButton");

            if (_navButtons.TryGetValue(page, out var navBtn))
            {
                navBtn.Style = (Style)FindResource("SidebarNavButtonActive");
                _activeNavButton = navBtn;
            }

            // Navigate to page
            Page targetPage = null;
            string titleText = page;

            switch (page)
            {
                case "Dashboard":
                    targetPage = new DashboardPage();
                    titleText = "Dashboard";
                    break;
                case "Projects":
                    targetPage = new ProjectsPage();
                    titleText = "Projects";
                    break;
                case "WorkReports":
                    targetPage = new WorkReportsPage();
                    titleText = "Work Reports";
                    break;
                case "Billing":
                    targetPage = new BillingPage();
                    titleText = "Billing";
                    break;
                case "Attendance":
                    targetPage = new AttendancePage();
                    titleText = "Attendance";
                    break;
                case "Leave":
                    targetPage = new LeavePage();
                    titleText = "Leave Management";
                    break;
                case "Quality":
                    targetPage = new QualityPage();
                    titleText = "Quality Reports";
                    break;
                case "Leaderboard":
                    targetPage = new LeaderboardPage();
                    titleText = "Leaderboard";
                    break;
                case "Messages":
                    targetPage = new MessagesPage();
                    titleText = "Messages";
                    break;
                case "BulkOps":
                    targetPage = new BulkOpsPage();
                    titleText = "Bulk Operations";
                    break;
                case "Users":
                    targetPage = new UsersPage();
                    titleText = "User Management";
                    break;
                case "Settings":
                    targetPage = new SettingsPage();
                    titleText = "Settings";
                    break;
            }

            if (targetPage != null)
            {
                MainFrame.Navigate(targetPage);
                PageTitleBar.Text = titleText;
            }
        }

        private void SignOut_Click(object sender, RoutedEventArgs e)
        {
            _clockTimer.Stop();
            SessionManager.Logout();
            var loginWin = new LoginWindow();
            Application.Current.MainWindow = loginWin;
            loginWin.Show();
            this.Close();
        }

        private void Profile_Click(object sender, RoutedEventArgs e)
        {
            var profileDialog = new ProfileDialog { Owner = this };
            profileDialog.ShowDialog();
            
            // Refresh user info after profile update
            var user = SessionManager.CurrentUser;
            if (user != null)
            {
                UserFullNameText.Text = user.FullName;
                UserInitialText.Text = (user.FullName?.Length > 0) ? user.FullName[0].ToString().ToUpper() : "U";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _clockTimer.Stop();
            base.OnClosed(e);
        }
    }
}
