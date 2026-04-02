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
    public partial class LeavePage : Page
    {
        private List<LeaveRequest> _allLeaves = new();

        public LeavePage()
        {
            InitializeComponent();
            // Set default selection after InitializeComponent to avoid XAML initialization events
            if (FilterStatus.Items.Count > 0)
                FilterStatus.SelectedIndex = 0;
            
            if (!SessionManager.IsAdmin)
            {
                EmployeeCol.Visibility = Visibility.Collapsed;
                ActionsCol.Visibility = Visibility.Collapsed;
                FilterEmployeeName.Visibility = Visibility.Collapsed;
            }
            
            LoadData();
        }

        private void LoadData()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                string query = SessionManager.IsAdmin ? @"
                    SELECT l.*, u.FullName AS EmployeeName
                    FROM Leaves l
                    JOIN Users u ON u.Id = l.UserId
                    ORDER BY l.StartDate DESC" : @"
                    SELECT l.*, u.FullName AS EmployeeName
                    FROM Leaves l
                    JOIN Users u ON u.Id = l.UserId
                    WHERE l.UserId = @UserId
                    ORDER BY l.StartDate DESC";

                if (SessionManager.IsAdmin)
                    _allLeaves = conn.Query<LeaveRequest>(query).AsList();
                else
                    _allLeaves = conn.Query<LeaveRequest>(query, new { UserId = SessionManager.CurrentUser.Id }).AsList();
            }
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (LeaveGrid == null) return;
            
            var filtered = _allLeaves.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(FilterEmployeeName.Text) && SessionManager.IsAdmin)
            {
                var query = FilterEmployeeName.Text.ToLower().Trim();
                filtered = filtered.Where(l => !string.IsNullOrEmpty(l.EmployeeName) && l.EmployeeName.ToLower().Contains(query));
            }

            if (FilterStatus.SelectedItem is ComboBoxItem item && item.Content.ToString() != "All Statuses")
                filtered = filtered.Where(l => l.Status == item.Content.ToString());

            if (FilterDateFrom.SelectedDate.HasValue)
                filtered = filtered.Where(l => l.StartDate.Date >= FilterDateFrom.SelectedDate.Value.Date);

            if (FilterDateTo.SelectedDate.HasValue)
                filtered = filtered.Where(l => l.EndDate.Date <= FilterDateTo.SelectedDate.Value.Date);

            LeaveGrid.ItemsSource = filtered.ToList();
        }

        private void Filter_Changed(object sender, EventArgs e) => ApplyFilters();

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            FilterEmployeeName.Text = "";
            FilterStatus.SelectedIndex = 0;
            FilterDateFrom.SelectedDate = null;
            FilterDateTo.SelectedDate = null;
        }

        private void ApplyLeave_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new LeaveDialog();
            if (dialog.ShowDialog() == true) LoadData();
        }

        private void Approve_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                UpdateLeaveStatus(id, "Approved");
            }
        }

        private void Reject_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                UpdateLeaveStatus(id, "Rejected");
            }
        }

        private void UpdateLeaveStatus(int id, string status)
        {
            using (var conn = DatabaseService.GetConnection())
            {
                conn.Execute("UPDATE Leaves SET Status=@Status WHERE Id=@Id", new { Status = status, Id = id });
                AuditService.Log("Leave Status Updated", $"Leave ID {id} set to {status}");
            }
            LoadData();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                if (MessageBox.Show("Delete this leave request?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    using (var conn = DatabaseService.GetConnection())
                    {
                        conn.Execute("DELETE FROM Leaves WHERE Id=@Id", new { Id = id });
                        AuditService.Log("Leave Request Deleted", $"Leave ID {id} deleted");
                    }
                    LoadData();
                }
            }
        }
    }
}
