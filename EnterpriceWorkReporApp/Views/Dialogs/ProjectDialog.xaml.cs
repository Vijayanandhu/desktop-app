using System.Windows;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport.Views.Dialogs
{
    public partial class ProjectDialog : Window
    {
        private readonly Project _project;
        private readonly bool _isEdit;

        public ProjectDialog(Project existing = null)
        {
            InitializeComponent();
            _project = existing ?? new Project();
            _isEdit = existing != null;

            if (_isEdit)
            {
                TitleText.Text = "Edit Project";
                NameBox.Text = _project.Name;
                DescriptionBox.Text = _project.Description;
                FormulaBox.Text = _project.BillingFormula;
                IsActiveCheck.IsChecked = _project.IsActive;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                ErrorText.Text = "Project name is required.";
                return;
            }

            using (var conn = DatabaseService.GetConnection())
            {
                if (_isEdit)
                {
                    conn.Execute("UPDATE Projects SET Name=@N, Description=@D, BillingFormula=@F, IsActive=@A WHERE Id=@Id",
                        new { N = NameBox.Text.Trim(), D = DescriptionBox.Text.Trim(), F = FormulaBox.Text.Trim(), A = IsActiveCheck.IsChecked == true ? 1 : 0, Id = _project.Id });
                    AuditService.Log("Project Updated", $"Project ID {_project.Id}: {NameBox.Text}");
                }
                else
                {
                    conn.Execute("INSERT INTO Projects (Name, Description, BillingFormula, IsActive) VALUES (@N, @D, @F, @A)",
                        new { N = NameBox.Text.Trim(), D = DescriptionBox.Text.Trim(), F = FormulaBox.Text.Trim(), A = 1 });
                    AuditService.Log("Project Created", $"Project: {NameBox.Text}");
                }
            }
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}
