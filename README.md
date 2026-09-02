# Smart File Import Service

Smart File Import Service is a small full-stack application for importing employee data from CSV and Excel files.

Users upload a `.csv` or `.xlsx` file from the React frontend. The backend saves the file into an incoming folder, a background worker processes it, valid employees are stored in SQL Server, and every import attempt is recorded in import history.

## Features

- Upload employee files from a web UI.
- Support CSV and XLSX formats.
- Queue uploaded files for background processing.
- Read employee rows from CSV files.
- Read employee rows from the first worksheet of Excel files.
- Validate required employee fields before saving.
- Store valid employees in SQL Server with Entity Framework Core.
- Record successful and failed imports.
- Move successful files to `Files/Processed`.
- Move failed files to `Files/Error`.
- Avoid overwriting moved files by adding numeric suffixes.
- Expose import history and dashboard API endpoints.
- Show upload, dashboard, history, and import details in the frontend.
- Refresh dashboard and history automatically after upload.
- Support light mode and dark mode in the frontend.
- Include unit and workflow tests for the backend.

## Technologies Used

- Backend: .NET 8, ASP.NET Core Web API
- Database: SQL Server / SQL Server Express
- Data access: Entity Framework Core
- Excel reader: ClosedXML
- Frontend: React 18, TypeScript, Vite
- Testing: xUnit, EF Core InMemory provider

## Architecture

```text
React frontend
    |
    | HTTP requests
    v
ASP.NET Core API
    |
    | saves uploaded files
    v
Files/Incoming
    |
    | scanned by FileImportWorker
    v
FileImportService
    |
    | reads CSV/XLSX, validates rows
    v
Entity Framework Core
    |
    v
SQL Server
```

The frontend does not process files directly. It only uploads files and displays the latest dashboard and history data from the API.

The backend has two main responsibilities:

- Accept uploads and save them into the configured incoming folder.
- Process queued files in the background and update the database.

## Project Structure

```text
SmartFileImport/
|-- Backend/
|   |-- Configuration/
|   |   `-- FileProcessingOptions.cs
|   |-- Controllers/
|   |   |-- DashboardController.cs
|   |   |-- FilesController.cs
|   |   |-- HealthController.cs
|   |   `-- ImportsController.cs
|   |-- Data/
|   |   |-- Migrations/
|   |   `-- ApplicationDbContext.cs
|   |-- Models/
|   |   |-- Employee.cs
|   |   `-- ImportHistory.cs
|   |-- Services/
|   |   |-- CsvFileReader.cs
|   |   |-- EmployeeValidator.cs
|   |   |-- ExcelFileReader.cs
|   |   |-- FileImportService.cs
|   |   |-- FileImportResult.cs
|   |   |-- ICsvFileReader.cs
|   |   |-- IEmployeeValidator.cs
|   |   |-- IExcelFileReader.cs
|   |   `-- IFileImportService.cs
|   |-- Workers/
|   |   `-- FileImportWorker.cs
|   |-- appsettings.json
|   |-- appsettings.Development.json
|   |-- Program.cs
|   `-- SmartFileImport.Api.csproj
|-- Backend.Tests/
|-- Files/
|   |-- Incoming/
|   |-- Processed/
|   `-- Error/
|-- Frontend/
|   |-- src/
|   |   |-- App.css
|   |   |-- App.tsx
|   |   `-- main.tsx
|   |-- index.html
|   |-- package.json
|   `-- vite.config.ts
|-- SmartFileImport.sln
`-- README.md
```

## Folder Structure

The file import folders live at the repository root:

```text
Files/Incoming
Files/Processed
Files/Error
```

The backend uses this configuration in `Backend/appsettings.json`:

```json
"FileProcessing": {
  "InputFolder": "../Files/Incoming",
  "ProcessedFolder": "../Files/Processed",
  "ErrorFolder": "../Files/Error",
  "ScanIntervalSeconds": 5
}
```

Folder usage:

- `Files/Incoming`: uploaded files waiting for processing.
- `Files/Processed`: files that were imported successfully.
- `Files/Error`: files that failed validation, parsing, saving, or processing.

## Supported File Formats

The application supports:

- `.csv`
- `.xlsx`

Both file types must contain employee data with these columns:

```text
Name
Email
Department
Salary
```

Validation rules:

- `Name` is required.
- `Email` is required and must be a valid email address.
- `Department` is required.
- `Salary` must be a number greater than zero.

Invalid files are not inserted into the `Employees` table. Failed imports are recorded in `ImportHistories`.

## Example CSV Format

```csv
Name,Email,Department,Salary
Ahmed Shablakh,ahmed@example.com,Engineering,4500.50
Sara Ali,sara@example.com,Finance,3900
```

## Example Excel Format

The first worksheet should contain the same headers in the first row:

```text
Name | Email | Department | Salary
```

Employee rows should start from the second row.

## Database Configuration

The backend uses SQL Server through Entity Framework Core.

Current connection string in `Backend/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost\\SQLEXPRESS01;Database=SmartFileImportDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;"
}
```

This configuration uses:

- SQL Server instance: `localhost\SQLEXPRESS01`
- Database name: `SmartFileImportDb`
- Authentication: Windows Authentication

If your SQL Server instance has a different name, update `DefaultConnection`.

Examples:

```json
"Server=MYPC;Database=SmartFileImportDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;"
```

```json
"Server=(localdb)\\MSSQLLocalDB;Database=SmartFileImportDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;"
```

The backend also supports this configuration key:

```json
"Database": {
  "Provider": "SqlServer",
  "ApplyMigrationsOnStartup": true
}
```

`Provider` can be:

- `SqlServer` for the real application.
- `InMemory` for tests or temporary local experiments.

In development, migrations are applied automatically on startup when `ApplyMigrationsOnStartup` is `true` and the provider is `SqlServer`.

## Database Tables

The database contains two main tables:

### Employees

Stores valid imported employees.

Columns:

- `Id`
- `Name`
- `Email`
- `Department`
- `Salary`
- `CreatedAt`

### ImportHistories

Stores every import attempt.

Columns:

- `Id`
- `FileName`
- `Status`
- `RecordCount`
- `ProcessedAt`
- `ErrorMessage`

`Status` is usually:

- `Success`
- `Failed`

## Database Migration Instructions

Install the EF Core CLI tool if needed:

```powershell
dotnet tool install --global dotnet-ef
```

Restore packages:

```powershell
dotnet restore SmartFileImport.sln
```

Apply the existing migration:

```powershell
dotnet ef database update --project Backend/SmartFileImport.Api.csproj --startup-project Backend/SmartFileImport.Api.csproj
```

Add a new migration only when the EF Core model changes:

```powershell
dotnet ef migrations add MigrationName --project Backend/SmartFileImport.Api.csproj --startup-project Backend/SmartFileImport.Api.csproj --output-dir Data/Migrations
```

## API Endpoints

Default backend URL:

```text
http://localhost:5107
```

Opening this URL in the browser returns a small API status response with the main endpoint paths.

### API Status

```http
GET /
```

Returns the API name, running status, frontend URL, and common endpoint paths.

### Health Check

```http
GET /api/health
```

Returns a simple response that confirms the API is running.

### Upload File

```http
POST /api/files/upload
```

Request type:

```text
multipart/form-data
```

Required form field:

```text
file
```

Successful uploads return `202 Accepted`:

```json
{
  "fileName": "employees.csv",
  "message": "File uploaded successfully and queued for background processing."
}
```

Possible validation errors return `400 Bad Request`, for example:

```json
{
  "error": "Only .csv and .xlsx files are supported."
}
```

### Import History

```http
GET /api/imports
```

Returns all import history records, newest first.

```json
[
  {
    "id": 1,
    "fileName": "employees.csv",
    "status": "Success",
    "recordCount": 2,
    "processedAt": "2026-09-02T07:10:00Z",
    "errorMessage": null
  }
]
```

### Import Details

```http
GET /api/imports/{id}
```

Returns one import history record.

If the record does not exist, the API returns `404 Not Found`.

### Dashboard Statistics

```http
GET /api/dashboard
```

Returns import summary statistics:

```json
{
  "totalFiles": 3,
  "successfulFiles": 2,
  "failedFiles": 1,
  "totalImportedRecords": 30
}
```

## Backend Setup

Prerequisites:

- .NET 8 SDK
- SQL Server, SQL Server Express, or LocalDB
- A working connection string in `Backend/appsettings.json`

Restore and build:

```powershell
dotnet restore SmartFileImport.sln
dotnet build SmartFileImport.sln
```

Run the backend:

```powershell
dotnet run --project Backend/SmartFileImport.Api.csproj --launch-profile http
```

The API should run at:

```text
http://localhost:5107
```

## Frontend Setup

Prerequisites:

- Node.js
- npm

Install packages:

```powershell
cd Frontend
npm install
```

Run the frontend:

```powershell
npm run dev
```

Open:

```text
http://localhost:5173
```

By default, the frontend calls:

```text
http://localhost:5107
```

To change the API URL for the current PowerShell session:

```powershell
$env:VITE_API_BASE_URL = "http://localhost:5107"
npm run dev
```

## How To Run The Application

### Option 1: Visual Studio 2022 plus Terminal

1. Open `SmartFileImport.sln` in Visual Studio 2022.
2. Set `Backend` as the startup project.
3. Select the `http` launch profile.
4. Start the backend with `F5` or `Ctrl+F5`.
5. Open `View > Terminal`.
6. Run the frontend:

```powershell
cd Frontend
npm install
npm run dev
```

7. Open `http://localhost:5173`.

### Option 2: Command Line Only

Terminal 1:

```powershell
dotnet run --project Backend/SmartFileImport.Api.csproj --launch-profile http
```

Terminal 2:

```powershell
cd Frontend
npm install
npm run dev
```

Open:

```text
http://localhost:5173
```

## How Background Processing Works

1. The user uploads a `.csv` or `.xlsx` file from the frontend.
2. `POST /api/files/upload` saves the file into `Files/Incoming`.
3. The API returns `202 Accepted`.
4. `FileImportWorker` scans the incoming folder every `ScanIntervalSeconds`.
5. The worker sends supported files to `IFileImportService`.
6. `FileImportService` selects the correct reader based on the file extension.
7. The reader maps rows to `Employee` objects.
8. `EmployeeValidator` validates all rows.
9. If the file is valid, employees are inserted into SQL Server.
10. An `ImportHistory` record is created.
11. Successful files are moved to `Files/Processed`.
12. Failed files are moved to `Files/Error`.
13. The frontend automatically refreshes dashboard and history after upload.

If one file fails, the worker logs the error and continues processing other files.

## Frontend Behavior

The frontend includes:

- File upload area.
- Required file structure guidance.
- Dashboard cards for total, successful, failed, and imported records.
- Import history table.
- Import details panel.
- Manual refresh button.
- Automatic refresh after upload.
- Light and dark mode toggle.

## Run Tests

Run backend tests:

```powershell
dotnet test SmartFileImport.sln
```

Build the frontend:

```powershell
npm --prefix Frontend run build
```

## Troubleshooting

### The frontend says it cannot reach the API

Make sure the backend is running at:

```text
http://localhost:5107
```

If the backend uses a different port, set `VITE_API_BASE_URL` before running the frontend.

### Import history or dashboard cannot load

Check the SQL Server connection string in `Backend/appsettings.json`.

Make sure the SQL Server instance exists and the Windows user running the backend has permission to create and use `SmartFileImportDb`.

### Uploaded file does not appear immediately

The upload API only queues the file. The background worker processes it on the next scan. The default scan interval is 5 seconds.

### Files are not in the expected folders

Check the `FileProcessing` section in `Backend/appsettings.json`.

The intended runtime folders are:

```text
Files/Incoming
Files/Processed
Files/Error
```
