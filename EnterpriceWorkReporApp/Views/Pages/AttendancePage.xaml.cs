using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;
using EnterpriseWorkReport.Views.Dialogs;
using Microsoft.Win32;

namespace EnterpriseWorkReport.Views.Pages
{
    public partial class AttendancePage : Page
    {
        private List<Attendance> _allRecords = new();

        public AttendancePage()
        {
            InitializeComponent();
            // Set default values after InitializeComponent to avoid XAML initialization events
            FilterDate.SelectedDate = DateTime.Today;
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
            try
            {
                using (var conn = DatabaseService.GetConnection())
                {
                    // Use raw query and manually map to avoid Dapper DateTime? issues with SQLite TEXT
                    var records = new List<Attendance>();
                    using (var cmd = new SQLiteCommand($@"
                        SELECT a.Id, a.UserId, a.Date, a.Status, a.Remarks,
                               a.ClockInTime, a.ClockOutTime, u.FullName AS EmployeeName
                        FROM Attendance a
                        JOIN Users u ON u.Id = a.UserId
                        {(!SessionManager.IsAdmin ? $"WHERE a.UserId = {SessionManager.CurrentUser.Id}" : "")}
                        ORDER BY a.Date DESC, u.FullName", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var rec = new Attendance
                            {
                                Id         = reader.GetInt32(0),
                                UserId     = reader.GetInt32(1),
                                Status     = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                Remarks    = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                EmployeeName = reader.IsDBNull(7) ? "" : reader.GetString(7)
                            };

                            // Date (TEXT in SQLite)
                            if (!reader.IsDBNull(2) && DateTime.TryParse(reader.GetString(2), out var dt))
                                rec.Date = dt;

                            // ClockInTime (TEXT in SQLite)
                            if (!reader.IsDBNull(5))
                            {
                                var raw = reader.GetString(5);
                                if (!string.IsNullOrWhiteSpace(raw) && DateTime.TryParse(raw, out var ci))
                                    rec.ClockInTime = ci;
                            }

                            // ClockOutTime (TEXT in SQLite)
                            if (!reader.IsDBNull(6))
                            {
                                var raw = reader.GetString(6);
                                if (!string.IsNullOrWhiteSpace(raw) && DateTime.TryParse(raw, out var co))
                                    rec.ClockOutTime = co;
                            }

                            records.Add(rec);
                        }
                    }
                    _allRecords = records;
                }
                ApplyFilters();
                UpdateClockButtons();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading attendance data:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void UpdateClockButtons()
        {
            // Check if user has already clocked in today
            var today = DateTime.Today;
            var userId = SessionManager.CurrentUser?.Id ?? 0;
            var todayRecord = _allRecords.FirstOrDefault(a => a.UserId == userId && a.Date.Date == today);
            
            if (todayRecord != null)
            {
                ClockInBtn.IsEnabled = string.IsNullOrEmpty(todayRecord.ClockInTime?.ToString());
                ClockOutBtn.IsEnabled = !string.IsNullOrEmpty(todayRecord.ClockInTime?.ToString()) && 
                                        string.IsNullOrEmpty(todayRecord.ClockOutTime?.ToString());
            }
            else
            {
                ClockInBtn.IsEnabled = true;
                ClockOutBtn.IsEnabled = false;
            }
        }

        private void ClockIn_Click(object sender, RoutedEventArgs e)
        {
            RecordClock(true);
        }

        private void ClockOut_Click(object sender, RoutedEventArgs e)
        {
            RecordClock(false);
        }

        private void RecordClock(bool isClockIn)
        {
            var userId = SessionManager.CurrentUser?.Id;
            if (userId == null)
            {
                MessageBox.Show("Please log in to record attendance.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var today = DateTime.Today;
            var now = DateTime.Now;

            using (var conn = DatabaseService.GetConnection())
            {
                // Check if record exists for today
                var existing = conn.QueryFirstOrDefault<Attendance>(
                    "SELECT * FROM Attendance WHERE UserId = @UserId AND Date = @Date",
                    new { UserId = userId, Date = today.ToString("yyyy-MM-dd") });

                if (existing == null)
                {
                    // Create new attendance record
                    conn.Execute(@"
                        INSERT INTO Attendance (UserId, Date, Status, ClockInTime, ClockOutTime, Remarks)
                        VALUES (@UserId, @Date, @Status, @ClockIn, @ClockOut, '')",
                        new { 
                            UserId = userId, 
                            Date = today.ToString("yyyy-MM-dd"), 
                            Status = "Present",
                            ClockIn = isClockIn ? now.ToString("yyyy-MM-dd HH:mm:ss") : null,
                            ClockOut = !isClockIn ? now.ToString("yyyy-MM-dd HH:mm:ss") : null
                        });
                    MessageBox.Show(isClockIn ? "Clocked in successfully!" : "Clocked out successfully!", 
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Update existing record
                    if (isClockIn && string.IsNullOrEmpty(existing.ClockInTime?.ToString()))
                    {
                        conn.Execute("UPDATE Attendance SET ClockInTime = @Time WHERE Id = @Id",
                            new { Time = now.ToString("yyyy-MM-dd HH:mm:ss"), Id = existing.Id });
                        MessageBox.Show("Clocked in successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (!isClockIn && string.IsNullOrEmpty(existing.ClockOutTime?.ToString()))
                    {
                        conn.Execute("UPDATE Attendance SET ClockOutTime = @Time WHERE Id = @Id",
                            new { Time = now.ToString("yyyy-MM-dd HH:mm:ss"), Id = existing.Id });
                        MessageBox.Show("Clocked out successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            LoadData();
        }

        private void ApplyFilters()
        {
            // Guard against null controls during XAML initialization
            if (AttendanceGrid == null) return;

            var filtered = _allRecords.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(FilterEmployeeName.Text) && SessionManager.IsAdmin)
            {
                var query = FilterEmployeeName.Text.ToLower().Trim();
                filtered = filtered.Where(a => !string.IsNullOrEmpty(a.EmployeeName) && a.EmployeeName.ToLower().Contains(query));
            }

            if (FilterDate.SelectedDate.HasValue)
                filtered = filtered.Where(a => a.Date.Date == FilterDate.SelectedDate.Value.Date);

            if (FilterStatus.SelectedItem is ComboBoxItem si)
            {
                string sel = si.Content?.ToString() ?? "";
                if (sel != "All Statuses" && !string.IsNullOrEmpty(sel))
                    filtered = filtered.Where(a => a.Status == sel);
            }

            AttendanceGrid.ItemsSource = filtered.ToList();
        }

        private void Filter_Changed(object sender, EventArgs e) => ApplyFilters();
        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            FilterDate.SelectedDate = null;
            FilterEmployeeName.Text = "";
            FilterStatus.SelectedIndex = 0;
        }

        private void MarkAttendance_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this);
            var dialog = new AttendanceDialog { Owner = mainWindow };
            if (dialog.ShowDialog() == true) LoadData();
        }

        private void EditAttendance_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                var rec = _allRecords.FirstOrDefault(a => a.Id == id);
                if (rec != null)
                {
                    var mainWindow = Window.GetWindow(this);
                    var dialog = new AttendanceDialog(rec) { Owner = mainWindow };
                    if (dialog.ShowDialog() == true) LoadData();
                }
            }
        }

        private void DeleteAttendance_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                if (MessageBox.Show("Delete this attendance record?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    using (var conn = DatabaseService.GetConnection())
                    {
                        conn.Execute("DELETE FROM Attendance WHERE Id=@Id", new { Id = id });
                    }
                    LoadData();
                }
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "CSV Files|*.csv", FileName = $"Attendance_{DateTime.Now:yyyyMMdd}.csv" };
            if (dlg.ShowDialog() != true) return;
            var rows = AttendanceGrid.ItemsSource as IEnumerable<Attendance> ?? _allRecords;
            using (var sw = new StreamWriter(dlg.FileName))
            {
                sw.WriteLine("Employee,Date,Status,Remarks");
                foreach (var r in rows)
                    sw.WriteLine($"{r.EmployeeName},{r.Date:dd/MM/yyyy},{r.Status},{r.Remarks}");
            }
            MessageBox.Show("Exported.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
