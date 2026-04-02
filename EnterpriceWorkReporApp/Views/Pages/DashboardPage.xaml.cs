using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;
using LiveCharts;
using LiveCharts.Wpf;

namespace EnterpriseWorkReport.Views.Pages
{
    public partial class DashboardPage : Page
    {
        public string[] ChartLabels { get; set; }
        public Func<double, string> ChartFormatter { get; set; }

        public DashboardPage()
        {
            InitializeComponent();
            DataContext = this;
            SubtitleText.Text = $"Good {GetGreeting()}, {SessionManager.CurrentUser?.FullName ?? "User"}";
            ChartFormatter = value => "₹" + value.ToString("N0");
            LoadData();
        }

        private string GetGreeting()
        {
            int hour = DateTime.Now.Hour;
            if (hour < 12) return "morning";
            if (hour < 17) return "afternoon";
            return "evening";
        }

        private void LoadData()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                // KPI totals
                TotalReportsText.Text = (conn.ExecuteScalar<long>("SELECT COUNT(*) FROM WorkReports")).ToString();
                var billing = conn.ExecuteScalar<double?>("SELECT SUM(BillingAmount) FROM WorkReports") ?? 0;
                TotalBillingText.Text = $"₹{billing:F2}";
                TotalEmployeesText.Text = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Users WHERE Role != 'Administrator' AND IsActive = 1").ToString();
                TotalProjectsText.Text = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Projects WHERE IsActive = 1").ToString();

                // Recent reports
                var recentReports = conn.Query<WorkReport>(@"
                    SELECT wr.*, u.FullName AS EmployeeName, p.Name AS ProjectName 
                    FROM WorkReports wr
                    JOIN Users u ON u.Id = wr.UserId
                    JOIN Projects p ON p.Id = wr.ProjectId
                    ORDER BY wr.SubmissionDate DESC LIMIT 20");
                RecentReportsGrid.ItemsSource = recentReports;

                // Top employees by billing
                var topEmployeesRaw = conn.Query(@"
                    SELECT u.FullName, SUM(wr.BillingAmount) AS BillingTotal
                    FROM WorkReports wr
                    JOIN Users u ON u.Id = wr.UserId
                    GROUP BY u.Id ORDER BY BillingTotal DESC LIMIT 10").ToList();

                var topEmployees = topEmployeesRaw.Select((row, idx) => new
                {
                    Rank = idx + 1,
                    FullName = ((IDictionary<string, object>)row)["FullName"],
                    BillingTotal = ((IDictionary<string, object>)row)["BillingTotal"]
                }).ToList();

                TopEmployeesList.ItemsSource = topEmployees;

                // Today's attendance
                string today = DateTime.Today.ToString("yyyy-MM-dd");
                PresentCountText.Text = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Attendance WHERE Date = @D AND Status='Present'", new { D = today }).ToString();
                AbsentCountText.Text = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Attendance WHERE Date = @D AND Status='Absent'", new { D = today }).ToString();
                HalfDayCountText.Text = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Attendance WHERE Date = @D AND Status='Half Day'", new { D = today }).ToString();
                LeaveCountText.Text = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Attendance WHERE Date = @D AND Status='Leave'", new { D = today }).ToString();

                // Charts: Weekly Billing Trend
                LoadWeeklyTrend(conn);
                
                // Charts: Project Distribution
                LoadProjectDistribution(conn);
            }
        }

        private void LoadWeeklyTrend(SQLiteConnection conn)
        {
            var last7Days = Enumerable.Range(0, 7).Select(i => DateTime.Today.AddDays(-i)).Reverse().ToList();
            ChartLabels = last7Days.Select(d => d.ToString("dd MMM")).ToArray();

            var trendData = conn.Query<(string Day, double Total)>(@"
                SELECT strftime('%Y-%m-%d', SubmissionDate) as Day, SUM(BillingAmount) as Total
                FROM WorkReports
                WHERE SubmissionDate >= @Start
                GROUP BY Day", new { Start = last7Days.First().ToString("yyyy-MM-dd") });

            var values = new ChartValues<double>();
            foreach (var day in last7Days)
            {
                var dayStr = day.ToString("yyyy-MM-dd");
                var match = trendData.FirstOrDefault(d => d.Day == dayStr);
                values.Add(match.Total);
            }

            BillingChart.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Daily Billing",
                    Values = values,
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 10,
                    StrokeThickness = 3,
                    Fill = new SolidColorBrush(Color.FromArgb(30, 33, 150, 243)) // Light blue fill
                }
            };
        }

        private void LoadProjectDistribution(SQLiteConnection conn)
        {
            var projectData = conn.Query<(string ProjectName, double Total)>(@"
                SELECT p.Name as ProjectName, SUM(wr.BillingAmount) as Total
                FROM WorkReports wr
                JOIN Projects p ON p.Id = wr.ProjectId
                GROUP BY p.Id ORDER BY Total DESC");

            var series = new SeriesCollection();
            foreach (var p in projectData)
            {
                series.Add(new PieSeries
                {
                    Title = p.ProjectName,
                    Values = new ChartValues<double> { p.Total },
                    DataLabels = true
                });
            }
            ProjectPieChart.Series = series;
        }
    }
}
