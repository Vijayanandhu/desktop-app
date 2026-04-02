using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using EnterpriseWorkReport.Models;

namespace EnterpriseWorkReport.Services
{
    /// <summary>
    /// Export service for generating reports in various formats
    /// </summary>
    public static class ExportService
    {
        /// <summary>
        /// Export work reports to HTML format (can be printed to PDF)
        /// </summary>
        public static void ExportWorkReportsToHtml(IEnumerable<WorkReport> reports, string filePath, string title = "Work Reports")
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head>");
            sb.AppendLine("<meta charset='UTF-8'>");
            sb.AppendLine($"<title>{title}</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; margin: 40px; }");
            sb.AppendLine("h1 { color: #1E88E5; border-bottom: 2px solid #1E88E5; padding-bottom: 10px; }");
            sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 20px; }");
            sb.AppendLine("th, td { border: 1px solid #ddd; padding: 12px; text-align: left; }");
            sb.AppendLine("th { background-color: #1E88E5; color: white; }");
            sb.AppendLine("tr:nth-child(even) { background-color: #f9f9f9; }");
            sb.AppendLine(".total { font-weight: bold; margin-top: 20px; }");
            sb.AppendLine("@media print { body { margin: 20px; } }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head><body>");
            sb.AppendLine($"<h1>{title}</h1>");
            sb.AppendLine($"<p>Generated: {DateTime.Now:dd/MM/yyyy HH:mm}</p>");
            sb.AppendLine("<table>");
            sb.AppendLine("<tr><th>ID</th><th>Employee</th><th>Project</th><th>Object ID</th><th>Billing Amount</th><th>Date</th></tr>");
            
            decimal total = 0;
            foreach (var r in reports)
            {
                total += r.BillingAmount;
                sb.AppendLine($"<tr><td>{r.Id}</td><td>{r.EmployeeName}</td><td>{r.ProjectName}</td><td>{r.ObjectId}</td><td>₹{r.BillingAmount:F2}</td><td>{r.SubmissionDate:dd/MM/yyyy}</td></tr>");
            }
            
            sb.AppendLine("</table>");
            sb.AppendLine($"<p class='total'>Total Billing: ₹{total:F2}</p>");
            sb.AppendLine("</body></html>");
            
            File.WriteAllText(filePath, sb.ToString());
        }

        /// <summary>
        /// Export attendance to HTML format
        /// </summary>
        public static void ExportAttendanceToHtml(IEnumerable<Attendance> records, string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head>");
            sb.AppendLine("<meta charset='UTF-8'>");
            sb.AppendLine("<title>Attendance Report</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; margin: 40px; }");
            sb.AppendLine("h1 { color: #1E88E5; border-bottom: 2px solid #1E88E5; padding-bottom: 10px; }");
            sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 20px; }");
            sb.AppendLine("th, td { border: 1px solid #ddd; padding: 12px; text-align: left; }");
            sb.AppendLine("th { background-color: #1E88E5; color: white; }");
            sb.AppendLine("tr:nth-child(even) { background-color: #f9f9f9; }");
            sb.AppendLine("@media print { body { margin: 20px; } }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head><body>");
            sb.AppendLine("<h1>Attendance Report</h1>");
            sb.AppendLine($"<p>Generated: {DateTime.Now:dd/MM/yyyy HH:mm}</p>");
            sb.AppendLine("<table>");
            sb.AppendLine("<tr><th>Employee</th><th>Date</th><th>Status</th><th>Remarks</th></tr>");
            
            foreach (var r in records)
            {
                sb.AppendLine($"<tr><td>{r.EmployeeName}</td><td>{r.Date:dd/MM/yyyy}</td><td>{r.Status}</td><td>{r.Remarks ?? "-"}</td></tr>");
            }
            
            sb.AppendLine("</table>");
            sb.AppendLine("</body></html>");
            
            File.WriteAllText(filePath, sb.ToString());
        }

        /// <summary>
        /// Export billing to HTML format
        /// </summary>
        public static void ExportBillingToHtml(IEnumerable<WorkReport> records, string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head>");
            sb.AppendLine("<meta charset='UTF-8'>");
            sb.AppendLine("<title>Billing Report</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; margin: 40px; }");
            sb.AppendLine("h1 { color: #1E88E5; border-bottom: 2px solid #1E88E5; padding-bottom: 10px; }");
            sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 20px; }");
            sb.AppendLine("th, td { border: 1px solid #ddd; padding: 12px; text-align: left; }");
            sb.AppendLine("th { background-color: #1E88E5; color: white; }");
            sb.AppendLine("tr:nth-child(even) { background-color: #f9f9f9; }");
            sb.AppendLine(".total { font-weight: bold; font-size: 18px; margin-top: 20px; color: #43A047; }");
            sb.AppendLine("@media print { body { margin: 20px; } }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head><body>");
            sb.AppendLine("<h1>Billing Report</h1>");
            sb.AppendLine($"<p>Generated: {DateTime.Now:dd/MM/yyyy HH:mm}</p>");
            sb.AppendLine("<table>");
            sb.AppendLine("<tr><th>Employee</th><th>Project</th><th>Object ID</th><th>Billing Amount (₹)</th><th>Date</th></tr>");
            
            decimal total = 0;
            foreach (var r in records)
            {
                total += r.BillingAmount;
                sb.AppendLine($"<tr><td>{r.EmployeeName}</td><td>{r.ProjectName}</td><td>{r.ObjectId}</td><td>₹{r.BillingAmount:F2}</td><td>{r.SubmissionDate:dd/MM/yyyy}</td></tr>");
            }
            
            sb.AppendLine("</table>");
            sb.AppendLine($"<p class='total'>Total Billing: ₹{total:F2}</p>");
            sb.AppendLine("</body></html>");
            
            File.WriteAllText(filePath, sb.ToString());
        }
    }
}
