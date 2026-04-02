using System;

namespace EnterpriseWorkReport.Models
{
    public class Attendance
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string EmployeeName { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; } // Present, Absent, Half Day, Leave
        public string Remarks { get; set; }
        
        // Clock In/Out times
        public DateTime? ClockInTime { get; set; }
        public DateTime? ClockOutTime { get; set; }
    }
}
