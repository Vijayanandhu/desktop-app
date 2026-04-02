using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport.Views.Dialogs
{
    public partial class LeaveDialog : Window
    {
        private List<User> _users = new();

        public LeaveDialog()
        {
            InitializeComponent();
            StartDate.SelectedDate = DateTime.Today;
            EndDate.SelectedDate = DateTime.Today;

            using (var conn = DatabaseService.GetConnection())
                _users = conn.Query<User>("SELECT * FROM Users WHERE IsActive=1 ORDER BY FullName").AsList();

            foreach (var u in _users)
                EmployeeCombo.Items.Add(new ComboBoxItem { Content = u.FullName, Tag = u.Id });

            // Default to current user
            var cur = SessionManager.CurrentUser;
            if (cur != null)
                foreach (ComboBoxItem item in EmployeeCombo.Items)
                    if ((int)item.Tag == cur.Id) { EmployeeCombo.SelectedItem = item; break; }
            if (EmployeeCombo.SelectedIndex < 0 && _users.Any())
                EmployeeCombo.SelectedIndex = 0;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (EmployeeCombo.SelectedItem is not ComboBoxItem emp) return;
            if (!StartDate.SelectedDate.HasValue || !EndDate.SelectedDate.HasValue) return;

            string leaveType = (LeaveTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            using (var conn = DatabaseService.GetConnection())
            {
                conn.Execute(@"INSERT INTO Leaves (UserId, LeaveType, StartDate, EndDate, Reason, Status)
                               VALUES (@U, @LT, @S, @E, @R, 'Pending')",
                    new
                    {
                        U = (int)emp.Tag,
                        LT = leaveType,
                        S = StartDate.SelectedDate.Value.ToString("yyyy-MM-dd"),
                        E = EndDate.SelectedDate.Value.ToString("yyyy-MM-dd"),
                        R = ReasonBox.Text.Trim()
                    });
            }
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}
