using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Windows;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport.Views.Dialogs
{
    public partial class UserDetailDialog : Window
    {
        private readonly User _user;
        private readonly int _userId;

        // Extended models for display
        private class LeaveRequestDisplay
        {
            public int Id { get; set; }
            public int UserId { get; set; }
            public string EmployeeName { get; set; }
            public string LeaveType { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public string Reason { get; set; }
            public string Status { get; set; }
            public int Days => (EndDate - StartDate).Days + 1;
        }

        private class WorkReportDisplay
        {
            public int Id { get; set; }
            public DateTime Date { get; set; }
            public string ProjectName { get; set; }
            public string ObjectId { get; set; }
            public decimal BillingAmount { get; set; }
        }

        private class QualityReportDisplay
        {
            public int Id { get; set; }
            public DateTime Date { get; set; }
            public double QualityScore { get; set; }
            public double ErrorRate { get; set; }
            public int ReworkCount { get; set; }
            public string Remarks { get; set; }
        }

        private class ProjectDisplay
        {
            public string ProjectName { get; set; }
            public int ReportsCount { get; set; }
            public decimal TotalAmount { get; set; }
            public DateTime? LastReportDate { get; set; }
        }

        public UserDetailDialog(User user)
        {
            InitializeComponent();
            _user = user;
            _userId = user.Id;
            LoadUserData();
        }

        public UserDetailDialog(int userId)
        {
            InitializeComponent();
            _userId = userId;
            LoadUserData();
        }

        private void LoadUserData()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                // Load user info
                if (_user != null)
                {
                    LoadUserInfo(_user);
                }
                else
                {
                    var user = conn.QueryFirstOrDefault<User>("SELECT * FROM Users WHERE Id = @Id", new { Id = _userId });
                    if (user != null)
                        LoadUserInfo(user);
                }

                // Load attendance data
                LoadAttendance(conn);

                // Load leave data
                LoadLeaveData(conn);

                // Load work reports
                LoadWorkReports(conn);

                // Load quality reports
                LoadQualityReports(conn);

                // Load projects
                LoadProjects(conn);
            }
        }

        private void LoadUserInfo(User user)
        {
            // Set avatar initials
            string initials = "";
            if (!string.IsNullOrWhiteSpace(user.FullName))
            {
                var parts = user.FullName.Split(' ');
                foreach (var part in parts)
                {
                    if (!string.IsNullOrWhiteSpace(part))
                        initials += part[0].ToString().ToUpper();
                    if (initials.Length >= 2) break;
                }
            }
            AvatarText.Text = initials;

            // Set basic info
            FullNameText.Text = user.FullName ?? "N/A";
            UsernameText.Text = "@" + (user.Username ?? "N/A");
            RoleText.Text = user.Role ?? "Employee";
            StatusText.Text = user.IsActive ? "Active" : "Inactive";

            // Update status badge color if inactive
            if (!user.IsActive)
            {
                StatusBadge.Style = (Style)FindResource("BadgeDanger");
                StatusText.Text = "Inactive";
                StatusText.Foreground = System.Windows.Media.Brushes.White;
            }
        }

        private void LoadAttendance(SQLiteConnection conn)
        {
            try
            {
                // Get current month range
                var now = DateTime.Now;
                var startOfMonth = new DateTime(now.Year, now.Month, 1);
                var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

                // Get attendance records for this month
                var attendance = conn.Query<Attendance>(
                    @"SELECT * FROM Attendance 
                      WHERE UserId = @UserId 
                      AND Date >= @StartDate 
                      AND Date <= @EndDate 
                      ORDER BY Date DESC",
                    new { UserId = _userId, StartDate = startOfMonth.ToString("yyyy-MM-dd"), EndDate = endOfMonth.ToString("yyyy-MM-dd") }
                ).ToList();

                // Calculate present/absent days
                int presentDays = attendance.Count(a => a.Status == "Present");
                int absentDays = attendance.Count(a => a.Status == "Absent");

                PresentDaysText.Text = presentDays.ToString();
                AbsentDaysText.Text = absentDays.ToString();

                // Also show today's status in the attendance tab
                AttendanceGrid.ItemsSource = attendance;

                // If no records, try to get all records
                if (!attendance.Any())
                {
                    var allAttendance = conn.Query<Attendance>(
                        @"SELECT * FROM Attendance 
                          WHERE UserId = @UserId 
                          ORDER BY Date DESC
                          LIMIT 50",
                        new { UserId = _userId }
                    ).ToList();
                    AttendanceGrid.ItemsSource = allAttendance;

                    // Recalculate for all time
                    presentDays = allAttendance.Count(a => a.Status == "Present");
                    absentDays = allAttendance.Count(a => a.Status == "Absent");
                    PresentDaysText.Text = presentDays.ToString();
                    AbsentDaysText.Text = absentDays.ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading attendance: {ex.Message}");
            }
        }

        private void LoadLeaveData(SQLiteConnection conn)
        {
            try
            {
                var leaves = conn.Query<LeaveRequestDisplay>(
                    @"SELECT * FROM Leaves 
                      WHERE UserId = @UserId 
                      ORDER BY StartDate DESC",
                    new { UserId = _userId }
                ).ToList();

                // Count by status
                int approved = leaves.Count(l => l.Status == "Approved");
                int pending = leaves.Count(l => l.Status == "Pending");

                ApprovedLeaveText.Text = approved.ToString();
                PendingLeaveText.Text = pending.ToString();

                LeaveGrid.ItemsSource = leaves;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading leave data: {ex.Message}");
            }
        }

        private void LoadWorkReports(SQLiteConnection conn)
        {
            try
            {
                var now = DateTime.Now;
                var startOfMonth = new DateTime(now.Year, now.Month, 1);
                var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

                // Get work reports for this month
                var reports = conn.Query<WorkReportDisplay>(
                    @"SELECT wr.Id, wr.SubmissionDate as Date, p.Name as ProjectName, wr.ObjectId, wr.BillingAmount
                      FROM WorkReports wr
                      LEFT JOIN Projects p ON wr.ProjectId = p.Id
                      WHERE wr.UserId = @UserId 
                      AND wr.SubmissionDate >= @StartDate 
                      AND wr.SubmissionDate <= @EndDate 
                      ORDER BY wr.SubmissionDate DESC",
                    new { UserId = _userId, StartDate = startOfMonth.ToString("yyyy-MM-dd"), EndDate = endOfMonth.ToString("yyyy-MM-dd") }
                ).ToList();

                decimal monthTotal = reports.Sum(r => r.BillingAmount);
                TotalReportsText.Text = reports.Count.ToString();
                TotalAmountText.Text = "₹" + monthTotal.ToString("N0");
                WorkReportsTotalAmount.Text = " - Total: ₹" + monthTotal.ToString("N2");

                // If no records this month, get all
                if (!reports.Any())
                {
                    reports = conn.Query<WorkReportDisplay>(
                        @"SELECT wr.Id, wr.SubmissionDate as Date, p.Name as ProjectName, wr.ObjectId, wr.BillingAmount
                          FROM WorkReports wr
                          LEFT JOIN Projects p ON wr.ProjectId = p.Id
                          WHERE wr.UserId = @UserId 
                          ORDER BY wr.SubmissionDate DESC",
                        new { UserId = _userId }
                    ).ToList();

                    var allTotal = reports.Sum(r => r.BillingAmount);
                    TotalAmountText.Text = "₹" + allTotal.ToString("N0");
                    WorkReportsTotalAmount.Text = " - Total: ₹" + allTotal.ToString("N2");
                }

                TotalReportsText.Text = reports.Count.ToString();
                WorkReportsGrid.ItemsSource = reports;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading work reports: {ex.Message}");
            }
        }

        private void LoadQualityReports(SQLiteConnection conn)
        {
            try
            {
                var reports = conn.Query<QualityReportDisplay>(
                    @"SELECT Id, ReportDate as Date, QualityScore, ErrorRate, ReworkCount, Remarks
                      FROM QualityReports 
                      WHERE UserId = @UserId 
                      ORDER BY ReportDate DESC",
                    new { UserId = _userId }
                ).ToList();

                // Calculate averages
                if (reports.Any())
                {
                    double avgQuality = reports.Average(r => r.QualityScore);
                    double avgError = reports.Average(r => r.ErrorRate);

                    AvgQualityText.Text = avgQuality.ToString("N1") + "%";
                    AvgErrorRateText.Text = avgError.ToString("N2") + "%";

                    // Color code error rate
                    if (avgError > 5)
                        AvgErrorRateText.Foreground = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC2626"));
                    else if (avgError > 2)
                        AvgErrorRateText.Foreground = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D97706"));
                    else
                        AvgErrorRateText.Foreground = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#059669"));
                }
                else
                {
                    AvgQualityText.Text = "N/A";
                    AvgErrorRateText.Text = "N/A";
                }

                TotalQualityReportsText.Text = reports.Count.ToString();
                QualitySummaryText.Text = $"Avg: {AvgQualityText.Text} | Error Rate: {AvgErrorRateText.Text}";
                QualityGrid.ItemsSource = reports;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading quality reports: {ex.Message}");
            }
        }

        private void LoadProjects(SQLiteConnection conn)
        {
            try
            {
                var projects = conn.Query<ProjectDisplay>(
                    @"SELECT p.Name as ProjectName, 
                             COUNT(wr.Id) as ReportsCount, 
                             COALESCE(SUM(wr.BillingAmount), 0) as TotalAmount,
                             MAX(wr.SubmissionDate) as LastReportDate
                      FROM Projects p
                      LEFT JOIN WorkReports wr ON p.Id = wr.ProjectId AND wr.UserId = @UserId
                      GROUP BY p.Id, p.Name
                      HAVING COUNT(wr.Id) > 0
                      ORDER BY ReportsCount DESC",
                    new { UserId = _userId }
                ).ToList();

                TotalProjectsText.Text = projects.Count.ToString();

                // Show project names in summary
                if (projects.Any())
                {
                    var projectNames = projects.Select(p => p.ProjectName).Take(3).ToList();
                    string names = string.Join(", ", projectNames);
                    if (projects.Count > 3)
                        names += $" (+{projects.Count - 3} more)";
                    ProjectsListText.Text = names;
                }
                else
                {
                    ProjectsListText.Text = "No projects yet";
                }

                ProjectsGrid.ItemsSource = projects;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading projects: {ex.Message}");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
