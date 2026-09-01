# Smart File Import Service

Smart File Import Service is a small backend and frontend project for managing CSV and Excel file imports.

This repository is being built one issue at a time. The current completed scope is listed below.

## Technologies

- Backend: .NET 8, ASP.NET Core Web API
- Frontend: React, TypeScript, Vite
- Data layer: Entity Framework Core with SQL Server
- Excel processing: ClosedXML

## Project Structure

```text
SmartFileImport/
|-- Backend/
|   |-- Controllers/
|   |   |-- DashboardController.cs
|   |   |-- FilesController.cs
|   |   |-- HealthController.cs
|   |   `-- ImportsController.cs
|   |-- Data/
|   |   `-- ApplicationDbContext.cs
|   |-- Configuration/
|   |   `-- FileProcessingOptions.cs
|   |-- Models/
|   |   |-- Employee.cs
|   |   `-- ImportHistory.cs
|   |-- Services/
|   |   |-- CsvFileReader.cs
|   |   |-- EmployeeValidator.cs
|   |   |-- ExcelFileReader.cs
|   |   |-- FileImportResult.cs
|   |   |-- FileImportService.cs
|   |   |-- ICsvFileReader.cs
|   |   |-- IEmployeeValidator.cs
|   |   |-- IFileImportService.cs
|   |   `-- IExcelFileReader.cs
|   |-- Workers/
|   |   `-- FileImportWorker.cs
|   |-- Program.cs
|   `-- SmartFileImport.Api.csproj
|-- Backend.Tests/
|   |-- CsvFileReaderTests.cs
|   |-- DashboardControllerTests.cs
|   |-- EmployeeValidatorTests.cs
|   |-- ExcelFileReaderTests.cs
|   |-- FileImportServiceTests.cs
|   |-- FileImportWorkerTests.cs
|   |-- FilesControllerTests.cs
|   |-- ImportsControllerTests.cs
|   `-- TestLogger.cs
|-- Frontend/
|   |-- src/
|   |-- index.html
|   |-- package.json
|   `-- vite.config.ts
|-- Files/
|   |-- Incoming/
|   |-- Processed/
|   `-- Error/
|-- SmartFileImport.sln
`-- README.md
```

## File Processing Folders

The import folders are created at the repository root:

- `Files/Incoming`
- `Files/Processed`
- `Files/Error`

The matching configuration lives in `Backend/appsettings.json`.

## File Upload API

Upload a CSV or Excel file:

```http
POST /api/files/upload
```

The request must use `multipart/form-data` with a form field named `file`.

Supported upload extensions are `.csv` and `.xlsx`. Valid uploads are saved to the configured incoming folder and return `202 Accepted` so background processing can handle the file later.

Unsupported, empty, or missing files return `400 Bad Request`.

## Frontend Upload UI

The upload screen lets a user select a `.csv` or `.xlsx` employee import file and send it to the backend upload API.

By default, the frontend calls the backend at `http://localhost:5107`.

Override the API base URL when needed:

```powershell
$env:VITE_API_BASE_URL = "http://localhost:5107"
npm run dev
```

## Frontend Import History UI

The history screen loads import records from the backend, displays them in a table, and shows details for the selected import.

The table displays:

- File name
- Status
- Record count
- Processed date

The details panel displays the selected import's error message when one exists.

## Database Configuration

The backend is configured for SQL Server through Entity Framework Core.

The default connection string is stored in `Backend/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=SmartFileImportDb;Trusted_Connection=True;TrustServerCertificate=True"
}
```

The `Employee` and `ImportHistory` entities are registered in `ApplicationDbContext`.

The initial migration is stored in `Backend/Data/Migrations`.

Apply migrations:

```powershell
dotnet ef database update --project Backend/SmartFileImport.Api.csproj --startup-project Backend/SmartFileImport.Api.csproj
```

Verify tables:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d SmartFileImportDb -Q "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME"
```

## CSV Format

CSV files must include these headers:

```csv
Name,Email,Department,Salary
Ahmed Shablakh,ahmed@example.com,Engineering,4500.50
```

The CSV reader maps rows to `Employee` objects. Employee validation is handled by the data validation service.

## Excel Format

Excel files must use `.xlsx` and include these headers in the first worksheet:

```text
Name | Email | Department | Salary
```

The Excel reader maps rows from the first worksheet to `Employee` objects. Employee validation is handled by the data validation service.

## Data Validation

Imported employees are validated with these rules:

- Name is required.
- Email must have a valid format.
- Department is required.
- Salary must be greater than zero.

The validator returns all detected errors with record numbers.

## File Import Service

The file import service runs the reusable import workflow:

```text
File
|-- Check file type
|-- Read CSV or Excel data
|-- Validate employees
`-- Save valid employees with EF Core
```

Supported import extensions are `.csv` and `.xlsx`.

If validation fails, the service returns clear validation errors and does not insert any employees.

## Import History Tracking

Every file import attempt creates an `ImportHistory` record.

Successful imports store:

- File name
- `Status = Success`
- Record count
- Processed timestamp

Failed imports store:

- File name
- `Status = Failed`
- Error message
- Processed timestamp

## Import History API

Get import history:

```http
GET /api/imports
```

Get one import history record:

```http
GET /api/imports/{id}
```

Each response item includes:

- `id`
- `fileName`
- `status`
- `recordCount`
- `processedAt`
- `errorMessage`

Missing import history records return `404 Not Found`.

## Dashboard API

Get dashboard statistics:

```http
GET /api/dashboard
```

The response is calculated from `ImportHistory` records:

```json
{
  "totalFiles": 3,
  "successfulFiles": 2,
  "failedFiles": 1,
  "totalImportedRecords": 5
}
```

The backend allows the local frontend dev server at `http://localhost:5173` and `https://localhost:5173` to call API endpoints.

## Background Processing

The backend registers `FileImportWorker` as a hosted background service.

The worker uses the `FileProcessing` configuration in `Backend/appsettings.json`:

- `InputFolder`
- `ProcessedFolder`
- `ErrorFolder`
- `ScanIntervalSeconds`

On each scan, the worker checks the incoming folder, detects `.csv` and `.xlsx` files, and sends supported files to `IFileImportService`.

If one file fails during processing, the worker catches the error and continues with the remaining files and future scans.

## Processed and Error File Handling

After processing a file, the worker moves it out of the incoming folder:

```text
Successful import -> Files/Processed
Failed import     -> Files/Error
```

The destination folders are created when needed. If a file with the same name already exists in the destination folder, the worker adds a numeric suffix instead of overwriting it.

If file movement fails, the error is handled safely and the worker continues processing later files.

## Logging and Exception Handling

The import workflow uses `ILogger` to log important processing steps:

- Worker startup, scans, detected files, and shutdown.
- File type detection and row counts during import.
- Validation failures with clear validation messages.
- Successful database saves and file moves.
- Import, scan, and file movement exceptions.

`FileImportService` logs import failures and rethrows them. `FileImportWorker` catches per-file exceptions, moves failed files to the error folder when possible, and continues processing the next file.

## Run The Backend

```powershell
dotnet restore SmartFileImport.sln
dotnet run --project Backend/SmartFileImport.Api.csproj
```

Health check:

```http
GET /api/health
```

## Run The Frontend

```powershell
cd Frontend
npm install
npm run dev
```

## Run Tests

```powershell
dotnet test SmartFileImport.sln
```

## Current Scope

Completed in Issue #1:

- Created the backend project.
- Created the frontend project.
- Created the base folder structure.
- Added `.gitignore`.
- Added initial setup documentation.

Completed in Issue #2:

- Added Entity Framework Core SQL Server packages.
- Created `ApplicationDbContext`.
- Added the SQL Server connection string.
- Registered the DbContext in the backend dependency injection container.

Completed in Issue #3:

- Created the `Employee` entity.
- Created the `ImportHistory` entity.
- Added `DbSet` properties to `ApplicationDbContext`.
- Configured table names, required fields, string lengths, and salary precision.

Completed in Issue #4:

- Created the initial EF Core migration.
- Applied the migration to SQL Server LocalDB.
- Verified that the required database tables exist.

Completed in Issue #5:

- Created the CSV file reader service.
- Mapped CSV columns to `Employee` properties.
- Added clear errors for missing headers, invalid salaries, unsupported extensions, and malformed CSV rows.
- Added focused unit tests for CSV parsing behavior.

Completed in Issue #6:

- Added ClosedXML for `.xlsx` file support.
- Created the Excel file reader service.
- Mapped worksheet columns to `Employee` properties.
- Added clear errors for missing headers, invalid salaries, unsupported extensions, and unreadable Excel files.
- Added focused unit tests for Excel parsing behavior.

Completed in Issue #7:

- Created the employee validation service.
- Added validation rules for name, email, department, and salary.
- Registered the validator in the backend dependency injection container.
- Added focused unit tests for validation behavior.

Completed in Issue #8:

- Created the reusable file import service.
- Added CSV and Excel reader selection by file extension.
- Validated imported employees before database insertion.
- Saved valid employees with Entity Framework Core.
- Registered the file import service in the backend dependency injection container.
- Added focused unit tests for import success, validation failure, and unsupported file types.

Completed in Issue #9:

- Created `FileImportWorker` as an ASP.NET Core hosted background service.
- Added periodic scanning using `FileProcessing:ScanIntervalSeconds`.
- Scanned the configured incoming folder for supported `.csv` and `.xlsx` files.
- Sent detected files to `IFileImportService`.
- Kept the worker running when one file fails during processing.
- Added focused unit tests for scanning, supported file detection, and error continuation.

Completed in Issue #10:

- Moved successfully processed files to the processed folder.
- Moved failed files to the error folder.
- Created destination folders when needed.
- Avoided overwriting existing destination files by adding numeric suffixes.
- Handled file movement errors safely so the worker keeps running.
- Added focused unit tests for processed moves, error moves, name collisions, and move failures.

Completed in Issue #11:

- Added structured logging to the file import service.
- Logged worker startup, scans, detected files, skipped files, and shutdown.
- Logged file type detection, row counts, validation failures, saves, and successful imports.
- Logged import, scan, and file movement exceptions with useful file context.
- Kept per-file exception handling in the worker so one failed file does not stop later files.
- Added focused test logging assertions for validation and import failure paths.

Completed in Issue #12:

- Recorded successful import attempts in `ImportHistory`.
- Recorded failed import attempts in `ImportHistory`.
- Stored failed import error messages.
- Stored successful import record counts.
- Added focused test assertions for successful and failed history records.

Completed in Issue #13:

- Added `POST /api/files/upload`.
- Accepted `.csv` and `.xlsx` uploads.
- Rejected unsupported, empty, and missing uploads.
- Saved uploaded files to the configured incoming folder.
- Kept actual file processing in the background worker.
- Added focused controller tests for upload success, rejection, and duplicate file names.

Completed in Issue #15:

- Added `GET /api/dashboard`.
- Calculated total files from import history records.
- Calculated successful and failed file counts.
- Calculated total imported records from successful imports.
- Enabled local frontend CORS access for API endpoints.
- Added focused dashboard controller tests for populated and empty databases.

Completed in Issue #17:

- Replaced the placeholder frontend screen with a file upload workspace.
- Added CSV and XLSX file selection.
- Connected the upload form to `POST /api/files/upload`.
- Displayed upload success and error messages from the API.
- Added local API base URL configuration through `VITE_API_BASE_URL`.

Completed in Issue #18:

- Added `GET /api/imports`.
- Added `GET /api/imports/{id}`.
- Returned import history file name, status, record count, processed date, and error message.
- Added a frontend import history table.
- Added a frontend import details panel.
- Added focused import history controller tests.

Not included yet:

- Frontend dashboard UI
