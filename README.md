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
|   |-- Workers/
|   |-- Program.cs
|   `-- SmartFileImport.Api.csproj
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
Migrations are intentionally not added yet. They belong to Issue #4.

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

Not included yet:

- Initial database migration
- File readers
- Background worker
- REST endpoints for uploads, imports, or dashboard data
