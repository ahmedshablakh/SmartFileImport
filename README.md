# Smart File Import Service

Smart File Import Service is a small backend and frontend project for managing CSV and Excel file imports.

This repository is being built one issue at a time. Issue #1 creates the project structure only: a runnable ASP.NET Core Web API, a runnable React TypeScript frontend, and the base import folders.

## Technologies

- Backend: .NET 8, ASP.NET Core Web API
- Frontend: React, TypeScript, Vite
- Data layer: Entity Framework Core with SQL Server

## Project Structure

```text
SmartFileImport/
|-- Backend/
|   |-- Configuration/
|   |-- Controllers/
|   |-- Data/
|   |   `-- ApplicationDbContext.cs
|   |-- Helpers/
|   |-- Models/
|   |   |-- Employee.cs
|   |   `-- ImportHistory.cs
|   |-- Services/
|   |   |-- CsvFileReader.cs
|   |   `-- ICsvFileReader.cs
|   |-- Workers/
|   |-- Program.cs
|   `-- SmartFileImport.Api.csproj
|-- Backend.Tests/
|   `-- CsvFileReaderTests.cs
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

The CSV reader maps rows to `Employee` objects. Full data validation is intentionally left for Issue #7.

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

Not included yet:

- Excel file reader
- Full data validation
- Background worker
- REST endpoints for uploads, imports, or dashboard data
