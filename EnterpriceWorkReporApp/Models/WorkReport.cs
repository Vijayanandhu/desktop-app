using System;
using System.Collections.Generic;

namespace EnterpriseWorkReport.Models
{
    public class WorkReport
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public int UserId { get; set; }
        public string ObjectId { get; set; }
        public DateTime SubmissionDate { get; set; } = DateTime.Now;
        public decimal BillingAmount { get; set; }
        public string AdminNote { get; set; }
        public string AttachmentPath { get; set; }
        
        // Navigation / display helpers
        public string ProjectName { get; set; }
        public string EmployeeName { get; set; }
        public bool HasAttachment => !string.IsNullOrEmpty(AttachmentPath);
        public string AttachmentFileName => string.IsNullOrEmpty(AttachmentPath) ? "" : System.IO.Path.GetFileName(AttachmentPath);
        public List<WorkReportItem> Items { get; set; } = new List<WorkReportItem>();
    }

    public class WorkReportItem
    {
        public int Id { get; set; }
        public int WorkReportId { get; set; }
        public int FieldId { get; set; }
        public string FieldLabel { get; set; }
        public string Value { get; set; }
    }
}
