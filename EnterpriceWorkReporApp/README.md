# 📊 Enterprise Work Report System (Pro Edition)

A professional, enterprise-grade WPF desktop application designed for high-performance workforce management, automated billing, and advanced analytics. Built for reliability, scalability, and a premium user experience.

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows_7%2B-lightgrey.svg)
![Stacks](https://img.shields.io/badge/stacks-WPF%20%7C%20SQLite%20%7C%20Dapper%20%7C%20QuestPDF-orange.svg)

---

## 🌟 Pro Features

- **🎨 Dynamic Tri-Theme Engine**: Switch instantly between **Professional Light**, **Midnight Navy**, and **AMOLED Pitch Black** modes without restarting.
- **📈 Modern Analytics Dashboard**: Interactive charts powered by `LiveCharts` visualizing weekly billing trends and project distribution.
- **📄 Professional PDF Export**: Dual-mode reporting system using `QuestPDF`.
  - **Summary Report**: Tabular overview for management.
  - **Detailed Report**: Professional single-page layouts with dynamic fields and signature areas for clients.
- **📎 File Attachment System**: Securely attach documents, images, or PDFs to any work report for verification.
- **🚀 One-Click Demo Mode**: Instantly populate the system with professional sample data for presentations, or perform a clean factory reset.

---

## 🚀 Core Capabilities

- **Automated Billing Engine**: Custom arithmetic formulas (e.g., `(WordCount / 1000) * Rate`) calculated in real-time.
- **Granular RBAC**: Role-Based Access Control for Administrators and Employees.
- **Workforce Management**: Attendance tracking, leave management, and quality performance monitoring.
- **Bulk Operations**: High-speed CSV/Excel imports/exports for all data modules.
- **Enterprise Security**: Full audit logs, secure password hashing, and automatic database backups.

---

## 💾 Database Configuration Guide

The application is designed to scale with your organization.

### 1. Local Database (Standalone)
**Default behavior.** The application creates a local `database.db` file in the `app_data/` folder. Best for single users or offline use.

### 2. Private Cloud Database (Dropbox/OneDrive/Google Drive)
**Best for small teams without a dedicated server.**
1. Install the application on all team computers.
2. Synchronize the `app_data/` folder via a cloud provider (e.g., Dropbox).
3. The application is optimized with **SQLite WAL (Write-Ahead Logging)** to handle concurrent access across cloud-synced folders.

### 3. Shared LAN Database (Office Server)
**Best for physical offices.**
1. Enable the **LAN Server** in the application's Settings on a central PC.
2. Other PCs can point their data path to the network share or access the server-hosted interface.

### 4. Enterprise Cloud Database (Azure SQL / AWS RDS / Postgres)
If you require massive scalability (1000+ users), you can migrate the data layer:
- **How to Connect**: 
  - Update the `ConnectionString` in `Services/DatabaseService.cs` to your cloud connection string.
  - Replace the `System.Data.SQLite` package with your provider (e.g., `Microsoft.Data.SqlClient`).
  - The application logic is decoupled from the provider via **Dapper**, making migration seamless.

---

## 🏁 Getting Started

### Prerequisites
- **Windows 7** or later.
- **.NET Framework 4.8** Runtime.

### Installation
1. Clone the repository: `git clone [Your-Repo-Link]`
2. Open the solution in **Visual Studio 2022**.
3. Restore NuGet packages and Build (**Ctrl+Shift+B**).
4. Launch `EnterpriseWorkReport.exe`.

### First Login (Admin)
- **Username**: `admin`
- **Password**: `admin123`
- *Note: You can use the **Demo Mode** in Settings to quickly see the app in action!*

---

## 👨‍💻 Portfolio & Contact

This application is a showcase of high-end C# / WPF architecture. 
- **Developer Website**: [Your-Website-Link-Here]
- **Key Skills Demonstrated**: Dynamic Schema Design, Multi-threaded UI, Modern Data Visualization, Custom Reporting Engines.

---

## 📄 License
This project is licensed under the **MIT License**.
