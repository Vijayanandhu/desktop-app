using System;
using System.IO;
using System.Windows;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;
using Microsoft.Win32;

namespace EnterpriseWorkReport.Views.Dialogs
{
    public partial class ProfileDialog : Window
    {
        private readonly AuthService _authService = new AuthService();
        private readonly User _currentUser;
        private string _profileImagePath = null;

        public ProfileDialog()
        {
            InitializeComponent();
            _currentUser = SessionManager.CurrentUser;
            LoadUserData();
        }

        private void LoadUserData()
        {
            if (_currentUser == null) return;

            FullNameBox.Text = _currentUser.FullName;
            EmailBox.Text = _currentUser.Email ?? "";
            PhoneBox.Text = _currentUser.Phone ?? "";
            DepartmentBox.Text = _currentUser.Department ?? "";
            DesignationBox.Text = _currentUser.Designation ?? "";
            ProfileInitials.Text = string.IsNullOrEmpty(_currentUser.FullName) ? "?" : _currentUser.FullName.Substring(0, 1).ToUpper();
            
            _profileImagePath = _currentUser.ProfilePicture;
            if (!string.IsNullOrEmpty(_profileImagePath) && File.Exists(_profileImagePath))
            {
                try
                {
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(_profileImagePath, UriKind.Absolute);
                    bmp.EndInit();
                    ProfileImagePreview.Source = bmp;
                    ProfileImagePreview.Visibility = Visibility.Visible;
                    ProfileInitials.Visibility = Visibility.Collapsed;
                }
                catch { }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Update profile information
            bool profileUpdated = _authService.UpdateProfile(
                _currentUser.Id,
                FullNameBox.Text.Trim(),
                EmailBox.Text.Trim(),
                PhoneBox.Text.Trim(),
                DepartmentBox.Text.Trim(),
                DesignationBox.Text.Trim(),
                _profileImagePath);

            // Handle password change if provided
            bool passwordChanged = false;
            string currentPwd = CurrentPasswordBox.Password;
            string newPwd = NewPasswordBox.Password;
            string confirmPwd = ConfirmPasswordBox.Password;

            if (!string.IsNullOrEmpty(currentPwd) || !string.IsNullOrEmpty(newPwd) || !string.IsNullOrEmpty(confirmPwd))
            {
                if (string.IsNullOrEmpty(currentPwd))
                {
                    ErrorText.Text = "Please enter your current password to change password.";
                    return;
                }
                if (string.IsNullOrEmpty(newPwd))
                {
                    ErrorText.Text = "Please enter a new password.";
                    return;
                }
                if (newPwd.Length < 6)
                {
                    ErrorText.Text = "New password must be at least 6 characters.";
                    return;
                }
                if (newPwd != confirmPwd)
                {
                    ErrorText.Text = "New password and confirmation do not match.";
                    return;
                }

                passwordChanged = _authService.ChangePassword(_currentUser.Id, currentPwd, newPwd);
                if (!passwordChanged)
                {
                    ErrorText.Text = "Current password is incorrect.";
                    return;
                }
            }

            if (profileUpdated || passwordChanged)
            {
                MessageBox.Show("Profile updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            else
            {
                ErrorText.Text = "No changes to save.";
            }
        }

        private void UploadPicture_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp",
                Title = "Select Profile Picture"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string uploadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EnterpriseWorkReport", "Uploads", "Images");
                    Directory.CreateDirectory(uploadsFolder);
                    
                    string ext = Path.GetExtension(dlg.FileName);
                    string targetPath = Path.Combine(uploadsFolder, $"user_{_currentUser.Id}_profile_{DateTime.Now.Ticks}{ext}");
                    File.Copy(dlg.FileName, targetPath, true);
                    
                    _profileImagePath = targetPath;
                    
                    // Show in preview
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(targetPath, UriKind.Absolute);
                    bmp.EndInit();
                    ProfileImagePreview.Source = bmp;
                    ProfileImagePreview.Visibility = Visibility.Visible;
                    ProfileInitials.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to upload picture: {ex.Message}", "Error");
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}