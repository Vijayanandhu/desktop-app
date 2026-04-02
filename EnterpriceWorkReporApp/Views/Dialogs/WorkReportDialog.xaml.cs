using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using EnterpriseWorkReport.Models;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport.Views.Dialogs
{
    public partial class WorkReportDialog : Window
    {
        private readonly WorkReport _report;
        private readonly bool _isEdit;
        private List<Project> _projects = new();
        private List<ProjectField> _currentFields = new();
        private readonly Dictionary<int, TextBox> _fieldInputs = new();
        private bool _anySaved = false;
        private string _attachmentPath = null;

        public WorkReportDialog(WorkReport existing = null)
        {
            InitializeComponent();
            _report = existing ?? new WorkReport { SubmissionDate = DateTime.Today };
            _isEdit = existing != null;

            if (_isEdit)
            {
                TitleText.Text = "Edit Work Report";
                _attachmentPath = _report.AttachmentPath;
                UpdateAttachmentUI();
            }
            
            ReportDate.SelectedDate = _report.SubmissionDate.Date;
            AdminNoteBox.Text = _report.AdminNote ?? "";

            if (!SessionManager.IsAdmin)
            {
                AdminNoteBox.IsReadOnly = true;
                AdminNoteBox.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(249, 250, 251)); // #F9FAFB
                if (string.IsNullOrWhiteSpace(_report.AdminNote))
                    AdminNotePanel.Visibility = Visibility.Collapsed;
            }

            LoadProjects();
        }

        private void LoadProjects()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                _projects = conn.Query<Project>("SELECT * FROM Projects WHERE IsActive=1 ORDER BY Name").AsList();
            }
            ProjectCombo.Items.Clear();
            foreach (var p in _projects)
                ProjectCombo.Items.Add(new ComboBoxItem { Content = p.Name, Tag = p.Id });

            if (_isEdit)
                foreach (ComboBoxItem item in ProjectCombo.Items)
                    if ((int)item.Tag == _report.ProjectId)
                    { ProjectCombo.SelectedItem = item; break; }
            else if (_projects.Any())
                ProjectCombo.SelectedIndex = 0;
        }

        private void Project_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProjectCombo.SelectedItem is ComboBoxItem item && item.Tag is int projectId)
                LoadDynamicFields(projectId);
        }

        private void LoadDynamicFields(int projectId)
        {
            DynamicFieldsPanel.Children.Clear();
            _fieldInputs.Clear();

            using (var conn = DatabaseService.GetConnection())
            {
                _currentFields = conn.Query<ProjectField>(
                    "SELECT * FROM ProjectFields WHERE ProjectId=@Id ORDER BY SortOrder",
                    new { Id = projectId }).AsList();
            }

            foreach (var field in _currentFields)
            {
                var label = new TextBlock
                {
                    Text = $"{field.FieldLabel.ToUpper()}{(field.IsRequired ? " *" : "")}",
                    Style = (Style)FindResource("FieldLabel"),
                    Margin = new Thickness(0, 12, 0, 6)
                };
                var input = new TextBox { Style = (Style)FindResource("ModernTextBox") };

                // Pre-fill on edit
                if (_isEdit)
                {
                    var existing = _report.Items.FirstOrDefault(i => i.FieldId == field.Id);
                    if (existing != null) input.Text = existing.Value;
                }

                DynamicFieldsPanel.Children.Add(label);
                DynamicFieldsPanel.Children.Add(input);
                _fieldInputs[field.Id] = input;
            }
        }

        private void CalculateBilling_Click(object sender, RoutedEventArgs e)
        {
            if (ProjectCombo.SelectedItem is not ComboBoxItem item) return;
            int projectId = (int)item.Tag;

            var project = _projects.FirstOrDefault(p => p.Id == projectId);
            if (string.IsNullOrWhiteSpace(project?.BillingFormula))
            {
                BillingBox.Text = "₹0.00";
                return;
            }

            try
            {
                // Build variable dictionary from numeric fields
                string formula = project.BillingFormula;
                foreach (var field in _currentFields.Where(f => f.FieldType == "Number"))
                {
                    if (_fieldInputs.TryGetValue(field.Id, out var input) &&
                        double.TryParse(input.Text, out double val))
                    {
                        formula = formula.Replace(field.FieldLabel, val.ToString());
                    }
                }

                var dt = new DataTable();
                var result = dt.Compute(formula, "");
                double billing = Convert.ToDouble(result);
                BillingBox.Text = $"₹{billing:F2}";
            }
            catch
            {
                BillingBox.Text = "Formula error";
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (ProjectCombo.SelectedItem is not ComboBoxItem item)
            { ErrorText.Text = "Please select a project."; return; }
            if (string.IsNullOrWhiteSpace(ObjectIdBox.Text))
            { ErrorText.Text = "Object ID is required."; return; }

            int projectId = (int)item.Tag;
            string objectId = ObjectIdBox.Text.Trim();

            // Check for duplicate Object ID (only for new reports or if Object ID changed)
            if (!_isEdit || objectId != _report.ObjectId)
            {
                using (var conn = DatabaseService.GetConnection())
                {
                    int existingCount = conn.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM WorkReports WHERE ProjectId = @ProjectId AND ObjectId = @ObjectId",
                        new { ProjectId = projectId, ObjectId = objectId });
                    
                    if (existingCount > 0)
                    {
                        ErrorText.Text = $"Object ID '{objectId}' already exists for this project. Please use a unique Object ID.";
                        return;
                    }
                }
            }

            // Validate required fields
            foreach (var field in _currentFields.Where(f => f.IsRequired))
            {
                if (_fieldInputs.TryGetValue(field.Id, out var inp) && string.IsNullOrWhiteSpace(inp.Text))
                {
                    ErrorText.Text = $"'{field.FieldLabel}' is required.";
                    return;
                }
            }

            // Parse billing
            double billing = 0;
            if (BillingBox.Text.StartsWith("₹") &&
                double.TryParse(BillingBox.Text.TrimStart('₹'), out double b))
                billing = b;

            using (var conn = DatabaseService.GetConnection())
            {
                int reportId;
                string adminNote = SessionManager.IsAdmin ? AdminNoteBox.Text.Trim() : _report.AdminNote;

                if (_isEdit)
                {
                    conn.Execute(@"UPDATE WorkReports SET ProjectId=@P, ObjectId=@O, SubmissionDate=@D, BillingAmount=@B, AdminNote=@A, AttachmentPath=@Att WHERE Id=@Id",
                        new { P = projectId, O = objectId, D = ReportDate.SelectedDate?.ToString("yyyy-MM-dd"), B = billing, A = adminNote, Att = _attachmentPath, Id = _report.Id });
                    conn.Execute("DELETE FROM WorkReportItems WHERE WorkReportId=@Id", new { Id = _report.Id });
                    reportId = _report.Id;
                }
                else
                {
                    conn.Execute(@"INSERT INTO WorkReports (ProjectId, UserId, ObjectId, SubmissionDate, BillingAmount, AdminNote, AttachmentPath)
                                   VALUES (@P, @U, @O, @D, @B, @A, @Att)",
                        new { P = projectId, U = SessionManager.CurrentUser.Id, O = objectId, D = ReportDate.SelectedDate?.ToString("yyyy-MM-dd"), B = billing, A = adminNote, Att = _attachmentPath });
                    reportId = conn.ExecuteScalar<int>("SELECT last_insert_rowid()");
                }

                // Save dynamic field values
                foreach (var kvp in _fieldInputs)
                {
                    conn.Execute("INSERT INTO WorkReportItems (WorkReportId, FieldId, Value) VALUES (@R, @F, @V)",
                        new { R = reportId, F = kvp.Key, V = kvp.Value.Text });
                }
            }

            AuditService.Log(_isEdit ? "Work Report Updated" : "Work Report Submitted", $"Object ID: {objectId}");
            
            if (!_isEdit && AddAnotherCheck.IsChecked == true)
            {
                _anySaved = true;
                ObjectIdBox.Text = "";
                BillingBox.Text = "";
                _attachmentPath = null;
                UpdateAttachmentUI();
                foreach (var input in _fieldInputs.Values) input.Text = "";
                
                ErrorText.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
                ErrorText.Text = "Report saved successfully! You can add another or click Close/Cancel.";
                ObjectIdBox.Focus();
            }
            else
            {
                DialogResult = true;
                Close();
            }
        }

        private void AttachFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Attachment",
                Filter = "All Files|*.*|Documents|*.pdf;*.docx;*.xlsx|Images|*.jpg;*.png"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string folder = System.IO.Path.Combine(DatabaseService.AppDataFolder, "Attachments");
                    System.IO.Directory.CreateDirectory(folder);
                    
                    string ext = System.IO.Path.GetExtension(dlg.FileName);
                    string target = System.IO.Path.Combine(folder, $"att_{DateTime.Now.Ticks}{ext}");
                    System.IO.File.Copy(dlg.FileName, target, true);
                    
                    _attachmentPath = target;
                    UpdateAttachmentUI();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to copy attachment: " + ex.Message);
                }
            }
        }

        private void RemoveAttachment_Click(object sender, RoutedEventArgs e)
        {
            _attachmentPath = null;
            UpdateAttachmentUI();
        }

        private void UpdateAttachmentUI()
        {
            if (string.IsNullOrEmpty(_attachmentPath))
            {
                AttachmentNameText.Text = "No file attached";
                RemoveAttachmentBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                AttachmentNameText.Text = System.IO.Path.GetFileName(_attachmentPath);
                RemoveAttachmentBtn.Visibility = Visibility.Visible;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) 
        { 
            if (_anySaved) DialogResult = true;
            else DialogResult = false; 
            Close(); 
        }
    }
}
