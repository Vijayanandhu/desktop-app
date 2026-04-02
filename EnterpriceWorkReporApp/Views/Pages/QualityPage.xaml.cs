using System;
using System.Collections.Generic;
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
    public partial class QualityPage : Page
    {
        private List<QualityReport> _allReports = new();
        private string _qualityReportsPath = "";

        public QualityPage()
        {
            InitializeComponent();
            LoadQualityReportsPath();
            
            if (!SessionManager.IsAdmin)
            {
                EmployeeCol.Visibility = Visibility.Collapsed;
                ActionsCol.Visibility = Visibility.Collapsed;
                FilterEmployeeName.Visibility = Visibility.Collapsed;
                AddQualityBtn.Visibility = Visibility.Collapsed;
                ImportBtn.Visibility = Visibility.Collapsed;
                // Folder path config only for Admin
                ReportPathBox.IsEnabled = false;
                SavePath_Click_Btn.Visibility = Visibility.Collapsed; 
            }
            
            LoadData();
        }

        private void LoadQualityReportsPath()
        {
            try
            {
                using (var conn = DatabaseService.GetConnection())
                {
                    var settings = conn.QueryFirstOrDefault<CompanySettings>(
                        "SELECT QualityReportsPath FROM CompanySettings WHERE Id = 1");
                    if (settings != null && !string.IsNullOrEmpty(settings.QualityReportsPath))
                    {
                        _qualityReportsPath = settings.QualityReportsPath;
                        ReportPathBox.Text = _qualityReportsPath;
                    }
                }
            }
            catch { }
        }

        private void SavePath_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string newPath = ReportPathBox.Text.Trim();
                using (var conn = DatabaseService.GetConnection())
                {
                    conn.Execute(
                        "UPDATE CompanySettings SET QualityReportsPath = @Path WHERE Id = 1",
                        new { Path = newPath });
                    _qualityReportsPath = newPath;
                    MessageBox.Show("Quality reports folder path saved!", "Success", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving path: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Filter_Changed(object sender, EventArgs e)
        {
            if (FilterDate?.SelectedDate != null)
            {
                DateTime selectedDate = FilterDate.SelectedDate.Value;
                LoadReportFromFolder(selectedDate);
            }
            ApplyFilters();
        }

        private void LoadReportFromFolder(DateTime date)
        {
            if (string.IsNullOrEmpty(_qualityReportsPath))
            {
                MessageBox.Show("Please configure the Quality Reports folder path first.", 
                    "Path Not Set", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Format: basePath\yyyymmdd\*.csv or basePath\mar_26\yyyymmdd\*.csv
                string dateFolder = date.ToString("yyyyMMdd");
                string searchPath = Path.Combine(_qualityReportsPath, dateFolder, "*.csv");
                
                // Try both formats: direct yyyymmdd or month folder (mar_26)
                var csvFiles = Directory.GetFiles(_qualityReportsPath, "*.csv", SearchOption.AllDirectories)
                    .Where(f => Path.GetFileName(f).Contains(dateFolder)).ToArray();
                
                if (csvFiles.Length == 0)
                {
                    // Try finding any CSV in the date folder
                    string datePath = Path.Combine(_qualityReportsPath, dateFolder);
                    if (Directory.Exists(datePath))
                    {
                        csvFiles = Directory.GetFiles(datePath, "*.csv");
                    }
                }

                if (csvFiles.Length == 0)
                {
                    MessageBox.Show($"No quality report found for date {date:dd/MM/yyyy}.\n\nExpected path: {searchPath}", 
                        "No Report", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Load the first matching CSV file
                var records = ParseCsvFile(csvFiles[0]);
                QualityGrid.ItemsSource = records;
                
                MessageBox.Show($"Loaded {records.Count} records from {Path.GetFileName(csvFiles[0])}", 
                    "Report Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading report: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<QualityReport> ParseCsvFile(string filePath)
        {
            var reports = new List<QualityReport>();
            var lines = File.ReadAllLines(filePath);
            
            if (lines.Length < 2) return reports;

            // Parse header to get column indices
            var header = lines[0].Replace("\"", "").Split(',').Select(h => h.Trim()).ToArray();
            
            int vendorIdx = Array.IndexOf(header, "Vendor_Name");
            int objectIdx = Array.IndexOf(header, "Object_Id");
            int pagesIdx = Array.IndexOf(header, "Total_Pages");
            int keyedIdx = Array.IndexOf(header, "Total_KeyedCharacters");
            int actualIdx = Array.IndexOf(header, "Total_ActualCharacters");
            int defectIdx = Array.IndexOf(header, "Total_DefectCharacters");
            int qualityIdx = Array.IndexOf(header, "Quality%");
            
            // Parse data rows
            for (int i = 1; i < lines.Length; i++)
            {
                var values = ParseCsvLine(lines[i]);
                if (values.Count == 0) continue;
                
                try
                {
                    string vendorName = vendorIdx >= 0 && vendorIdx < values.Count ? values[vendorIdx] : "";
                    string objectId = objectIdx >= 0 && objectIdx < values.Count ? values[objectIdx] : "";
                    int totalPages = pagesIdx >= 0 && pagesIdx < values.Count ? int.TryParse(values[pagesIdx], out var tp) ? tp : 0 : 0;
                    int keyedChars = keyedIdx >= 0 && keyedIdx < values.Count ? int.TryParse(values[keyedIdx], out var kc) ? kc : 0 : 0;
                    int actualChars = actualIdx >= 0 && actualIdx < values.Count ? int.TryParse(values[actualIdx], out var ac) ? ac : 0 : 0;
                    int defectChars = defectIdx >= 0 && defectIdx < values.Count ? int.TryParse(values[defectIdx], out var dc) ? dc : 0 : 0;
                    double qualityPct = qualityIdx >= 0 && qualityIdx < values.Count ? double.TryParse(values[qualityIdx].Replace("%", ""), out var qp) ? qp : 0 : 0;
                    
                    // Calculate accuracy if not provided
                    double accuracy = qualityPct;
                    double errorRate = 0;
                    if (actualChars > 0)
                    {
                        accuracy = ((double)(actualChars - defectChars) / actualChars) * 100;
                        errorRate = ((double)defectChars / actualChars) * 100;
                    }
                    
                    var report = new QualityReport
                    {
                        EmployeeName = vendorName,
                        ReportDate = DateTime.Today,
                        QualityScore = qualityPct > 0 ? qualityPct : accuracy,
                        Accuracy = accuracy,
                        ErrorRate = errorRate,
                        ReworkCount = defectChars > 0 ? 1 : 0,
                        Remarks = $"Object: {objectId}, Pages: {totalPages}"
                    };
                    reports.Add(report);
                }
                catch { }
            }
            
            return reports;
        }

        private List<string> ParseCsvLine(string line)
        {
            var values = new List<string>();
            bool inQuotes = false;
            string currentValue = "";
            
            foreach (char c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    values.Add(currentValue.Trim());
                    currentValue = "";
                }
                else
                {
                    currentValue += c;
                }
            }
            values.Add(currentValue.Trim());
            return values;
        }



        private void LoadData()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                _allReports = conn.Query<QualityReport>(@"
                    SELECT qr.*, u.FullName AS EmployeeName
                    FROM QualityReports qr
                    JOIN Users u ON u.Id = qr.UserId
                    ORDER BY qr.ReportDate DESC").AsList();
            }
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (QualityGrid == null) return;
            
            var filtered = _allReports.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(FilterEmployeeName.Text) && SessionManager.IsAdmin)
            {
                var query = FilterEmployeeName.Text.ToLower().Trim();
                filtered = filtered.Where(q => !string.IsNullOrEmpty(q.EmployeeName) && q.EmployeeName.ToLower().Contains(query));
            }

            if (FilterDate.SelectedDate.HasValue)
                filtered = filtered.Where(q => q.ReportDate.Date >= FilterDate.SelectedDate.Value.Date);

            if (FilterDateTo.SelectedDate.HasValue)
                filtered = filtered.Where(q => q.ReportDate.Date <= FilterDateTo.SelectedDate.Value.Date);

            QualityGrid.ItemsSource = filtered.ToList();
        }



        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            FilterEmployeeName.Text = "";
            FilterDate.SelectedDate = null;
            FilterDateTo.SelectedDate = null;
        }

        private void EditQuality_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                var report = _allReports.FirstOrDefault(r => r.Id == id);
                if (report != null)
                {
                    var dialog = new QualityReportDialog(report);
                    if (dialog.ShowDialog() == true) LoadData();
                }
            }
        }

        private void DeleteQuality_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                if (MessageBox.Show("Delete this quality report?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    using (var conn = DatabaseService.GetConnection())
                    {
                        conn.Execute("DELETE FROM QualityReports WHERE Id=@Id", new { Id = id });
                        AuditService.Log("Quality Report Deleted", $"Record ID {id} deleted");
                    }
                    LoadData();
                }
            }
        }

        private void AddQuality_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new QualityReportDialog();
            if (dialog.ShowDialog() == true) LoadData();
        }

        private void ImportFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ImportQualityReportDialog { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true)
            {
                LoadData();
                MessageBox.Show("Quality reports imported successfully!", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
