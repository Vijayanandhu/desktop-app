using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public partial class WorkReportsPage : Page
    {
        private List<WorkReport> _allReports = new();

        public WorkReportsPage()
        {
            InitializeComponent();
            LoadProjectFilter();
            // Set default selection after InitializeComponent to avoid XAML initialization events
            if (FilterProject.Items.Count > 0)
                FilterProject.SelectedIndex = 0;
            
            if (!SessionManager.IsAdmin)
            {
                EmployeeCol.Visibility = Visibility.Collapsed;
                FilterEmployeeName.Visibility = Visibility.Collapsed;
            }
            
            LoadReports();
        }

        private void LoadProjectFilter()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                var projects = conn.Query<Project>("SELECT * FROM Projects WHERE IsActive=1 ORDER BY Name").AsList();
                FilterProject.Items.Clear();
                FilterProject.Items.Add(new ComboBoxItem { Content = "All Projects", IsSelected = true });
                foreach (var p in projects)
                    FilterProject.Items.Add(new ComboBoxItem { Content = p.Name, Tag = p.Id });
            }
        }

        private void LoadReports()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                // Regular users can only see their own reports
                string query = SessionManager.IsAdmin ? @"
                    SELECT wr.*, u.FullName AS EmployeeName, p.Name AS ProjectName 
                    FROM WorkReports wr
                    JOIN Users u ON u.Id = wr.UserId
                    JOIN Projects p ON p.Id = wr.ProjectId
                    ORDER BY wr.SubmissionDate DESC" : @"
                    SELECT wr.*, u.FullName AS EmployeeName, p.Name AS ProjectName 
                    FROM WorkReports wr
                    JOIN Users u ON u.Id = wr.UserId
                    JOIN Projects p ON p.Id = wr.ProjectId
                    WHERE wr.UserId = @UserId
                    ORDER BY wr.SubmissionDate DESC";
                
                if (SessionManager.IsAdmin)
                {
                    _allReports = conn.Query<WorkReport>(query).AsList();
                }
                else
                {
                    _allReports = conn.Query<WorkReport>(query, new { UserId = SessionManager.CurrentUser.Id }).AsList();
                }
            }
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            // Guard against null controls during XAML initialization
            if (ReportsGrid == null) return;
            
            var filtered = _allReports.AsEnumerable();

            if (FilterProject.SelectedItem is ComboBoxItem item && item.Tag is int projectId)
                filtered = filtered.Where(r => r.ProjectId == projectId);

            if (!string.IsNullOrWhiteSpace(FilterEmployeeName.Text) && SessionManager.IsAdmin)
            {
                var query = FilterEmployeeName.Text.ToLower().Trim();
                filtered = filtered.Where(r => !string.IsNullOrEmpty(r.EmployeeName) && r.EmployeeName.ToLower().Contains(query));
            }

            if (FilterDateFrom.SelectedDate.HasValue)
                filtered = filtered.Where(r => r.SubmissionDate.Date >= FilterDateFrom.SelectedDate.Value.Date);

            if (FilterDateTo.SelectedDate.HasValue)
                filtered = filtered.Where(r => r.SubmissionDate.Date <= FilterDateTo.SelectedDate.Value.Date);

            ReportsGrid.ItemsSource = filtered.ToList();
        }

        private void Filter_Changed(object sender, EventArgs e) => ApplyFilters();

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            FilterProject.SelectedIndex = 0;
            FilterEmployeeName.Text = "";
            FilterDateFrom.SelectedDate = null;
            FilterDateTo.SelectedDate = null;
        }

        private void AddReport_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this);
            var dialog = new WorkReportDialog { Owner = mainWindow };
            if (dialog.ShowDialog() == true)
                LoadReports();
        }

        private void EditReport_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                var report = _allReports.FirstOrDefault(r => r.Id == id);
                if (report != null)
                {
                    var mainWindow = Window.GetWindow(this);
                    var dialog = new WorkReportDialog(report) { Owner = mainWindow };
                    if (dialog.ShowDialog() == true)
                        LoadReports();
                }
            }
        }

        private void DeleteReport_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                if (MessageBox.Show("Delete this report?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    using (var conn = DatabaseService.GetConnection())
                    {
                        conn.Execute("DELETE FROM WorkReportItems WHERE WorkReportId=@Id", new { Id = id });
                        conn.Execute("DELETE FROM WorkReports WHERE Id=@Id", new { Id = id });
                    }
                    LoadReports();
                }
            }
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "CSV Files|*.csv", FileName = $"WorkReports_{DateTime.Now:yyyyMMdd}.csv" };
            if (dlg.ShowDialog() != true) return;

            var rows = ReportsGrid.ItemsSource as IEnumerable<WorkReport> ?? _allReports;
            using (var sw = new StreamWriter(dlg.FileName))
            {
                sw.WriteLine("ID,Employee,Project,Object ID,Billing Amount,Date");
                foreach (var r in rows)
                    sw.WriteLine($"{r.Id},{r.EmployeeName},{r.ProjectName},{r.ObjectId},₹{r.BillingAmount:F2},{r.SubmissionDate:dd/MM/yyyy}");
            }
            MessageBox.Show("Exported successfully.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportSummaryPdf_Click(object sender, RoutedEventArgs e)
        {
            var reports = (ReportsGrid.ItemsSource as IEnumerable<WorkReport>)?.ToList() ?? _allReports;
            if (!reports.Any()) { MessageBox.Show("No data to export.", "Empty", MessageBoxButton.OK, MessageBoxImage.Information); return; }

            var dlg = new SaveFileDialog { Filter = "PDF Files|*.pdf", FileName = $"WorkReport_Summary_{DateTime.Now:yyyyMMdd}.pdf" };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var service = new ReportingService();
                service.ExportWorkReportSummaryPdf(reports, dlg.FileName);
                Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to export PDF: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportDetailedPdf_Click(object sender, RoutedEventArgs e)
        {
            var reports = ReportsGrid.SelectedItems.Cast<WorkReport>().ToList();
            if (!reports.Any()) 
                reports = (ReportsGrid.ItemsSource as IEnumerable<WorkReport>)?.ToList() ?? _allReports;
            
            if (!reports.Any()) { MessageBox.Show("No data to export.", "Empty", MessageBoxButton.OK, MessageBoxImage.Information); return; }

            var dlg = new SaveFileDialog { Filter = "PDF Files|*.pdf", FileName = $"WorkReport_Detailed_{DateTime.Now:yyyyMMdd}.pdf" };
            if (dlg.ShowDialog() != true) return;

            try
            {
                // We need to ensure dynamic items are loaded for each report if they aren't already
                using (var conn = DatabaseService.GetConnection())
                {
                    foreach (var report in reports)
                    {
                        if (report.Items == null || !report.Items.Any())
                        {
                            report.Items = conn.Query<WorkReportItem>(@"
                                SELECT wri.*, pf.FieldLabel 
                                FROM WorkReportItems wri
                                JOIN ProjectFields pf ON pf.Id = wri.FieldId
                                WHERE wri.WorkReportId = @Id", new { Id = report.Id }).AsList();
                        }
                    }
                }

                var service = new ReportingService();
                service.ExportWorkReportDetailedPdf(reports, dlg.FileName);
                Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to export Detailed PDF: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ViewAttachment_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is string path && !string.IsNullOrEmpty(path))
            {
                if (File.Exists(path))
                {
                    try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
                    catch (Exception ex) { MessageBox.Show("Could not open file: " + ex.Message); }
                }
                else
                {
                    MessageBox.Show("Attachment file not found. It may have been moved or deleted.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }
}
