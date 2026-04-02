using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;
using EnterpriseWorkReport.Views.Dialogs;

namespace EnterpriseWorkReport.Views.Pages
{
    public partial class UsersPage : Page
    {
        private List<User> _allUsers = new();

        public UsersPage()
        {
            InitializeComponent();
            
            // Security check: Only admins can manage users
            if (!SessionManager.IsAdmin)
            {
                MessageBox.Show("Access Denied. Only administrators can manage users.", 
                    "Unauthorized", MessageBoxButton.OK, MessageBoxImage.Warning);
                NavigationService?.GoBack();
                return;
            }
            
            // Set default selection after InitializeComponent to avoid XAML initialization events
            if (FilterRole.Items.Count > 0)
                FilterRole.SelectedIndex = 0;
            
            LoadUsers();
        }

        private void LoadUsers()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                _allUsers = conn.Query<User>("SELECT * FROM Users ORDER BY FullName").AsList();
            }
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            // Guard against null controls during XAML initialization
            if (UsersGrid == null) return;
            
            var filtered = _allUsers.AsEnumerable();

            if (FilterRole.SelectedItem is ComboBoxItem ri && ri.Content.ToString() != "All Roles")
                filtered = filtered.Where(u => u.Role == ri.Content.ToString());

            string search = SearchBox.Text.ToLower();
            if (!string.IsNullOrWhiteSpace(search))
                filtered = filtered.Where(u => u.FullName.ToLower().Contains(search) || u.Username.ToLower().Contains(search));

            UsersGrid.ItemsSource = filtered.ToList();
        }

        private void Filter_Changed(object sender, EventArgs e) => ApplyFilters();
        private void Search_Changed(object sender, TextChangedEventArgs e) => ApplyFilters();

        private void AddUser_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this);
            var dialog = new UserDialog { Owner = mainWindow };
            if (dialog.ShowDialog() == true) LoadUsers();
        }

        private void EditUser_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                var user = _allUsers.FirstOrDefault(u => u.Id == id);
                if (user != null)
                {
                    var mainWindow = Window.GetWindow(this);
                    var dialog = new UserDialog(user) { Owner = mainWindow };
                    if (dialog.ShowDialog() == true) LoadUsers();
                }
            }
        }

        private void ResetPassword_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                var user = _allUsers.FirstOrDefault(u => u.Id == id);
                string newPwd = "password123";
                string hashedPwd = AuthService.HashPassword(newPwd);
                using (var conn = DatabaseService.GetConnection())
                    conn.Execute("UPDATE Users SET PasswordHash=@P WHERE Id=@Id", new { P = hashedPwd, Id = id });
                AuditService.Log("Password Reset", $"Admin reset password for user: {user?.FullName}");
                MessageBox.Show($"Password for {user?.FullName} has been reset to: {newPwd}", "Reset Password", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeactivateUser_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                var user = _allUsers.FirstOrDefault(u => u.Id == id);
                if (MessageBox.Show($"Deactivate user '{user?.FullName}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    using (var conn = DatabaseService.GetConnection())
                        conn.Execute("UPDATE Users SET IsActive=0 WHERE Id=@Id", new { Id = id });
                    LoadUsers();
                }
            }
        }

        private void ViewUserDetails_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is User user)
            {
                var mainWindow = Window.GetWindow(this);
                var dialog = new UserDetailDialog(user) { Owner = mainWindow };
                dialog.ShowDialog();
            }
        }
    }
}
