using System;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport.Views.Dialogs
{
    public partial class UserDialog : Window
    {
        private readonly User _user;
        private readonly bool _isEdit;

        public UserDialog(User existing = null)
        {
            InitializeComponent();
            _user = existing ?? new User();
            _isEdit = existing != null;

            if (_isEdit)
            {
                TitleText.Text = "Edit User";
                FullNameBox.Text = _user.FullName;
                UsernameBox.Text = _user.Username;
                UsernameBox.IsReadOnly = true;
                PasswordBox.Password = ""; // Don't show password

                foreach (ComboBoxItem item in RoleCombo.Items)
                    if (item.Content.ToString() == _user.Role)
                    { RoleCombo.SelectedItem = item; break; }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FullNameBox.Text)) { ErrorText.Text = "Full name required."; return; }
            if (string.IsNullOrWhiteSpace(UsernameBox.Text)) { ErrorText.Text = "Username required."; return; }
            if (!_isEdit && string.IsNullOrWhiteSpace(PasswordBox.Password)) { ErrorText.Text = "Password required."; return; }

            string role = (RoleCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Employee";
            string password = !string.IsNullOrWhiteSpace(PasswordBox.Password) ? PasswordBox.Password : _user.PasswordHash;

            using (var conn = DatabaseService.GetConnection())
            {
                if (_isEdit)
                {
                    conn.Execute("UPDATE Users SET FullName=@FN, Role=@R, PasswordHash=@P WHERE Id=@Id",
                        new { FN = FullNameBox.Text.Trim(), R = role, P = password, Id = _user.Id });
                    AuditService.Log("User Updated", $"User ID {_user.Id}: {FullNameBox.Text}");
                }
                else
                {
                    int existing = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Users WHERE Username=@U", new { U = UsernameBox.Text.Trim() });
                    if (existing > 0) { ErrorText.Text = "Username already exists."; return; }

                    conn.Execute("INSERT INTO Users (Username, FullName, PasswordHash, Role, IsActive) VALUES (@U, @FN, @P, @R, 1)",
                        new { U = UsernameBox.Text.Trim(), FN = FullNameBox.Text.Trim(), P = password, R = role });
                    AuditService.Log("User Created", $"New user: {UsernameBox.Text}");
                }
            }
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        private void GeneratePassword_Click(object sender, RoutedEventArgs e)
        {
            // Generate a memorable password pattern: FirstName123!
            string firstName = FullNameBox.Text.Trim();
            if (string.IsNullOrEmpty(firstName))
            {
                GeneratedPasswordText.Text = "Enter full name first";
                GeneratedPasswordText.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }
            
            // Get first name (first word)
            string baseName = firstName.Split(' ')[0];
            baseName = baseName.ToLower();
            
            // Generate pattern: FirstName + year + special char
            string year = DateTime.Now.Year.ToString().Substring(2); // Last 2 digits
            string[] special = { "!", "@", "#", "$", "%", "&" };
            Random rnd = new Random();
            string specialChar = special[rnd.Next(special.Length)];
            
            // Create memorable password: John@123
            string password = $"{baseName}{specialChar}{rnd.Next(10, 99)}";
            
            PasswordBox.Password = password;
            GeneratedPasswordText.Text = $"Generated: {password}";
            GeneratedPasswordText.Foreground = System.Windows.Media.Brushes.Green;
        }
    }
}
