using System;

namespace EnterpriseWorkReport.Models
{
    public class CompanySettings
    {
        public int Id { get; set; }
        public string CompanyName { get; set; }
        public string CompanyAddress { get; set; }
        public string CompanyPhone { get; set; }
        public string CompanyEmail { get; set; }
        public string TaxId { get; set; }
        public string CurrencySymbol { get; set; }
        public string TimeZone { get; set; }
        public string LogoPath { get; set; }
        public string QualityReportsPath { get; set; } // Base path for quality report CSV files
        public DateTime UpdatedAt { get; set; }
    }
}
