using System;
using System.Data.SQLite;
using System.IO;

namespace EnterpriseWorkReport.Services
{
    public static class DatabaseService
    {
        public static string AppDataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_data");
        public static string DbPath = Path.Combine(AppDataFolder, "database.db");
        public static string BackupsFolder = Path.Combine(AppDataFolder, "backups");
        
        // Enhanced connection string for multi-user support with WAL mode
        // WAL (Write-Ahead Logging) allows concurrent reads while writing
        public static string ConnectionString = $"Data Source={DbPath};Version=3;BusyTimeout=30000;Journal Mode=WAL;Pooling=True;Max Pool Size=100;";

        public static void InitializeDatabase()
        {
            if (!Directory.Exists(AppDataFolder))
                Directory.CreateDirectory(AppDataFolder);
            
            if (!Directory.Exists(BackupsFolder))
                Directory.CreateDirectory(BackupsFolder);

            if (!File.Exists(DbPath))
            {
                SQLiteConnection.CreateFile(DbPath);
            }

            using (var connection = new SQLiteConnection(ConnectionString))
            {
                connection.Open();
                
                // Enable WAL mode for better multi-user concurrency
                using (var cmd = new SQLiteCommand("PRAGMA journal_mode=WAL;", connection))
                {
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = new SQLiteCommand("PRAGMA busy_timeout=30000;", connection))
                {
                    cmd.ExecuteNonQuery();
                }
                
                CreateTables(connection);
                SeedInitialAdmin(connection);
            }
        }

        public static SQLiteConnection GetConnection()
        {
            try
            {
                if (!Directory.Exists(AppDataFolder))
                    Directory.CreateDirectory(AppDataFolder);

                if (!File.Exists(DbPath))
                    SQLiteConnection.CreateFile(DbPath);

                var conn = new SQLiteConnection(ConnectionString);
                conn.Open();
                return conn;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unable to open database at '{DbPath}'. Please check permissions and file lock status.", ex);
            }
        }

        private static void CreateTables(SQLiteConnection connection)
        {
            string createUsersTableQuery = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL UNIQUE,
                    PasswordHash TEXT NOT NULL,
                    Role TEXT NOT NULL,
                    FullName TEXT NOT NULL,
                    IsActive INTEGER DEFAULT 1,
                    Email TEXT,
                    Phone TEXT,
                    Department TEXT,
                    Designation TEXT,
                    ProfilePicture TEXT,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    LastLoginAt DATETIME
                );";

            string createProjectsTableQuery = @"
                CREATE TABLE IF NOT EXISTS Projects (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL UNIQUE,
                    Description TEXT,
                    IsActive INTEGER DEFAULT 1,
                    BillingFormula TEXT
                );";

            string createProjectFieldsTableQuery = @"
                CREATE TABLE IF NOT EXISTS ProjectFields (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProjectId INTEGER NOT NULL,
                    FieldLabel TEXT NOT NULL,
                    FieldType TEXT NOT NULL,
                    IsRequired INTEGER DEFAULT 0,
                    IncludeInBilling INTEGER DEFAULT 0,
                    SortOrder INTEGER DEFAULT 0,
                    FOREIGN KEY(ProjectId) REFERENCES Projects(Id)
                );";

            string createWorkReportsTableQuery = @"
                CREATE TABLE IF NOT EXISTS WorkReports (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProjectId INTEGER NOT NULL,
                    UserId INTEGER NOT NULL,
                    SubmissionDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    ObjectId TEXT NOT NULL, 
                    BillingAmount REAL DEFAULT 0,
                    AttachmentPath TEXT,
                    AdminNote TEXT,
                    FOREIGN KEY(ProjectId) REFERENCES Projects(Id),
                    FOREIGN KEY(UserId) REFERENCES Users(Id)
                );";

            string createWorkReportItemsTableQuery = @"
                CREATE TABLE IF NOT EXISTS WorkReportItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    WorkReportId INTEGER NOT NULL,
                    FieldId INTEGER NOT NULL,
                    Value TEXT,
                    FOREIGN KEY(WorkReportId) REFERENCES WorkReports(Id),
                    FOREIGN KEY(FieldId) REFERENCES ProjectFields(Id)
                );";

            string createAttendanceTableQuery = @"
                CREATE TABLE IF NOT EXISTS Attendance (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    Date DATE NOT NULL,
                    Status TEXT NOT NULL,
                    Remarks TEXT,
                    ClockInTime TEXT,
                    ClockOutTime TEXT,
                    FOREIGN KEY(UserId) REFERENCES Users(Id)
                );";

            string createLeavesTableQuery = @"
                CREATE TABLE IF NOT EXISTS Leaves (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    LeaveType TEXT NOT NULL,
                    StartDate DATE NOT NULL,
                    EndDate DATE NOT NULL,
                    Reason TEXT,
                    Status TEXT DEFAULT 'Pending',
                    FOREIGN KEY(UserId) REFERENCES Users(Id)
                );";

            string createQualityReportsTable = @"
                CREATE TABLE IF NOT EXISTS QualityReports (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    ReportDate DATE NOT NULL,
                    Accuracy REAL DEFAULT 0,
                    ErrorRate REAL DEFAULT 0,
                    ReworkCount INTEGER DEFAULT 0,
                    QualityScore REAL DEFAULT 0,
                    Remarks TEXT,
                    FOREIGN KEY(UserId) REFERENCES Users(Id)
                );";

            string createAuditLogsTable = @"
                CREATE TABLE IF NOT EXISTS AuditLogs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER,
                    Username TEXT,
                    Action TEXT NOT NULL,
                    Details TEXT,
                    Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
                );";

            string createBillingRecordsTable = @"
                CREATE TABLE IF NOT EXISTS BillingRecords (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    WorkReportId INTEGER NOT NULL,
                    UserId INTEGER NOT NULL,
                    ProjectId INTEGER NOT NULL,
                    Amount REAL DEFAULT 0,
                    CalculatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(WorkReportId) REFERENCES WorkReports(Id)
                );";

            // Messages table for user-to-user and admin messaging
            string createMessagesTable = @"
                CREATE TABLE IF NOT EXISTS Messages (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SenderId INTEGER NOT NULL,
                    ReceiverId INTEGER,
                    Subject TEXT,
                    Content TEXT NOT NULL,
                    AttachmentPath TEXT,
                    AttachmentType TEXT,
                    IsRead INTEGER DEFAULT 0,
                    IsBroadcast INTEGER DEFAULT 0,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(SenderId) REFERENCES Users(Id),
                    FOREIGN KEY(ReceiverId) REFERENCES Users(Id)
                );
                
                CREATE INDEX IF NOT EXISTS idx_messages_receiver ON Messages(ReceiverId);
                CREATE INDEX IF NOT EXISTS idx_messages_sender ON Messages(SenderId);";

            // Company settings table
            string createCompanySettingsTable = @"
                CREATE TABLE IF NOT EXISTS CompanySettings (
                    Id INTEGER PRIMARY KEY CHECK (Id = 1),
                    CompanyName TEXT DEFAULT 'My Company',
                    CompanyAddress TEXT,
                    CompanyPhone TEXT,
                    CompanyEmail TEXT,
                    TaxId TEXT,
                    CurrencySymbol TEXT DEFAULT '₹',
                    TimeZone TEXT DEFAULT 'India Standard Time',
                    LogoPath TEXT,
                    QualityReportsPath TEXT,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );
                
                INSERT OR IGNORE INTO CompanySettings (Id, CompanyName) VALUES (1, 'My Company');";

            using (var command = new SQLiteCommand(connection))
            {
                command.CommandText = createUsersTableQuery;
                command.ExecuteNonQuery();

                command.CommandText = createProjectsTableQuery;
                command.ExecuteNonQuery();

                command.CommandText = createProjectFieldsTableQuery;
                command.ExecuteNonQuery();

                command.CommandText = createWorkReportsTableQuery;
                command.ExecuteNonQuery();

                command.CommandText = createWorkReportItemsTableQuery;
                command.ExecuteNonQuery();
                
                command.CommandText = createAttendanceTableQuery;
                command.ExecuteNonQuery();

                command.CommandText = createLeavesTableQuery;
                command.ExecuteNonQuery();

                command.CommandText = createQualityReportsTable;
                command.ExecuteNonQuery();

                command.CommandText = createAuditLogsTable;
                command.ExecuteNonQuery();

                command.CommandText = createBillingRecordsTable;
                command.ExecuteNonQuery();
                
                command.CommandText = createMessagesTable;
                command.ExecuteNonQuery();
                
                command.CommandText = createCompanySettingsTable;
                command.ExecuteNonQuery();
            }
            
            // Run migrations for existing databases
            RunMigrations(connection);
        }
        
        private static void RunMigrations(SQLiteConnection connection)
        {
            // Add ClockInTime and ClockOutTime columns if they don't exist (for existing databases)
            try
            {
                using (var cmd = new SQLiteCommand("ALTER TABLE Attendance ADD COLUMN ClockInTime TEXT", connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            catch { } // Column may already exist
            
            try
            {
                using (var cmd = new SQLiteCommand("ALTER TABLE Attendance ADD COLUMN ClockOutTime TEXT", connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            catch { } // Column may already exist

            try
            {
                using (var cmd = new SQLiteCommand("ALTER TABLE WorkReports ADD COLUMN AdminNote TEXT", connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            catch { } // Column may already exist
            
            try
            {
                using (var cmd = new SQLiteCommand("ALTER TABLE WorkReports ADD COLUMN AttachmentPath TEXT", connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            catch { } // Column may already exist
            
            // Add QualityReportsPath column if it doesn't exist
            try
            {
                using (var cmd = new SQLiteCommand("ALTER TABLE CompanySettings ADD COLUMN QualityReportsPath TEXT", connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            catch { } // Column may already exist
        }

        private static void SeedInitialAdmin(SQLiteConnection connection)
        {
            string checkAdminQuery = "SELECT COUNT(*) FROM Users WHERE Username = 'admin'";
            using (var checkCommand = new SQLiteCommand(checkAdminQuery, connection))
            {
                long count = (long)checkCommand.ExecuteScalar();
                if (count == 0)
                {
                    // Default password is 'admin123' (hashed)
                    string hashedPassword = AuthService.HashPassword("admin123");
                    string insertAdminQuery = @"
                        INSERT INTO Users (Username, PasswordHash, Role, FullName, Email, Department, Designation) 
                        VALUES ('admin', @Password, 'Administrator', 'System Admin', 'admin@company.com', 'IT', 'System Administrator')";
                    using (var insertCommand = new SQLiteCommand(insertAdminQuery, connection))
                    {
                        insertCommand.Parameters.AddWithValue("@Password", hashedPassword);
                        insertCommand.ExecuteNonQuery();
                    }
                }
            }
        }
    }
}
