using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport.Views.Dialogs
{
    public partial class ProjectFieldsDialog : Window
    {
        private readonly Project _project;

        public ProjectFieldsDialog(Project project)
        {
            InitializeComponent();
            _project = project;
            TitleText.Text = $"Fields – {project.Name}";
            LoadFields();
        }

        private void LoadFields()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                var fields = conn.Query<ProjectField>(
                    "SELECT * FROM ProjectFields WHERE ProjectId=@Id ORDER BY SortOrder",
                    new { Id = _project.Id }).AsList();
                FieldsGrid.ItemsSource = fields;
            }
        }

        private void AddField_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewFieldLabelBox.Text)) return;

            int maxSort = 0;
            using (var conn = DatabaseService.GetConnection())
            {
                maxSort = conn.ExecuteScalar<int>("SELECT COALESCE(MAX(SortOrder),0) FROM ProjectFields WHERE ProjectId=@Id", new { Id = _project.Id });
                conn.Execute(@"INSERT INTO ProjectFields (ProjectId, FieldLabel, FieldType, IsRequired, IncludeInBilling, SortOrder)
                               VALUES (@ProjectId, @Label, @Type, @Req, @Billing, @Sort)",
                    new
                    {
                        ProjectId = _project.Id,
                        Label = NewFieldLabelBox.Text.Trim(),
                        Type = (NewFieldTypeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Text",
                        Req = NewFieldRequired.IsChecked == true ? 1 : 0,
                        Billing = NewFieldBilling.IsChecked == true ? 1 : 0,
                        Sort = maxSort + 1
                    });
            }

            NewFieldLabelBox.Clear();
            NewFieldRequired.IsChecked = false;
            NewFieldBilling.IsChecked = false;
            LoadFields();
        }

        private void DeleteField_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int id)
            {
                using (var conn = DatabaseService.GetConnection())
                    conn.Execute("DELETE FROM ProjectFields WHERE Id=@Id", new { Id = id });
                LoadFields();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
