using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport.Views.Pages
{
    public partial class LeaderboardPage : Page
    {
        private bool _isInitialized = false;
        
        public LeaderboardPage()
        {
            InitializeComponent();
            // Set default selection after InitializeComponent to avoid XAML initialization events
            if (PeriodSelector.Items.Count > 0)
                PeriodSelector.SelectedIndex = 0;
            _isInitialized = true;
            LoadLeaderboard();
        }

        private void Period_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitialized)
                LoadLeaderboard();
        }

        private void LoadLeaderboard()
        {
            // Guard against null controls during XAML initialization
            if (LeaderboardGrid == null || !_isInitialized) return;

            try
            {
                string dateFilterCondition = "";
                var period = (PeriodSelector.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Today";

                switch (period)
                {
                    case "Today":
                        dateFilterCondition = " AND DATE(wr.SubmissionDate) = DATE('now')";
                        break;
                    case "This Week":
                        dateFilterCondition = " AND DATE(wr.SubmissionDate) >= DATE('now', '-7 days')";
                        break;
                    case "This Month":
                        dateFilterCondition = " AND strftime('%Y-%m', wr.SubmissionDate) = strftime('%Y-%m', 'now')";
                        break;
                    case "All Time":
                    default:
                        dateFilterCondition = "";
                        break;
                }

                // Build SQL without window functions (ROW_NUMBER OVER is unreliable in older SQLite)
                // Date filter goes inside the LEFT JOIN condition so the role/active filter always applies
                string sql = $@"
                    SELECT u.FullName,
                           COALESCE(SUM(wr.BillingAmount), 0) AS BillingTotal,
                           COUNT(wr.Id) AS ReportCount
                    FROM Users u
                    LEFT JOIN WorkReports wr ON wr.UserId = u.Id{dateFilterCondition}
                    WHERE u.Role != 'Administrator' AND u.IsActive = 1
                    GROUP BY u.Id, u.FullName
                    ORDER BY BillingTotal DESC";

                using (var conn = DatabaseService.GetConnection())
                {
                    var rawResults = conn.Query(sql).AsList();

                    // Add rank on the C# side — immune to SQLite version differences
                    var results = rawResults.Select((row, idx) => new
                    {
                        Rank         = idx + 1,
                        FullName     = (string)((IDictionary<string, object>)row)["FullName"],
                        BillingTotal = Convert.ToDouble(((IDictionary<string, object>)row)["BillingTotal"]),
                        ReportCount  = Convert.ToInt32(((IDictionary<string, object>)row)["ReportCount"])
                    }).ToList();

                    LeaderboardGrid.ItemsSource = results;

                    // Update podium
                    if (Rank1Name != null && Rank1Billing != null && Rank1Initial != null)
                        UpdatePodium(results.Count > 0 ? (object)results[0] : null, Rank1Name, Rank1Billing, Rank1Initial);
                    if (Rank2Name != null && Rank2Billing != null && Rank2Initial != null)
                        UpdatePodium(results.Count > 1 ? (object)results[1] : null, Rank2Name, Rank2Billing, Rank2Initial);
                    if (Rank3Name != null && Rank3Billing != null && Rank3Initial != null)
                        UpdatePodium(results.Count > 2 ? (object)results[2] : null, Rank3Name, Rank3Billing, Rank3Initial);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading leaderboard:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdatePodium(object entry, TextBlock nameBlock, TextBlock billingBlock, TextBlock initialBlock)
        {
            if (entry == null)
            {
                nameBlock.Text = "—";
                billingBlock.Text = "₹0";
                initialBlock.Text = "?";
                return;
            }

            // Works for both dynamic Dapper rows and anonymous types
            string fullName = "";
            double billingTotal = 0;
            if (entry is IDictionary<string, object> dict)
            {
                fullName = dict.ContainsKey("FullName") ? dict["FullName"]?.ToString() ?? "" : "";
                billingTotal = dict.ContainsKey("BillingTotal") ? Convert.ToDouble(dict["BillingTotal"]) : 0;
            }
            else
            {
                // Anonymous type via reflection
                var t = entry.GetType();
                fullName = t.GetProperty("FullName")?.GetValue(entry)?.ToString() ?? "";
                var bt = t.GetProperty("BillingTotal")?.GetValue(entry);
                billingTotal = bt != null ? Convert.ToDouble(bt) : 0;
            }

            nameBlock.Text = string.IsNullOrEmpty(fullName) ? "—" : fullName;
            billingBlock.Text = $"₹{billingTotal:F2}";
            initialBlock.Text = fullName.Length > 0 ? fullName[0].ToString().ToUpper() : "?";
        }
    }
}
