using Microsoft.EntityFrameworkCore;
using SmartFileImport.Api.Configuration;
using SmartFileImport.Api.Data;
using SmartFileImport.Api.Services;
using SmartFileImport.Api.Workers;

const string FrontendCorsPolicy = "FrontendCorsPolicy";

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var databaseProvider = builder.Configuration.GetValue<string>("Database:Provider") ?? "SqlServer";

if (string.Equals(databaseProvider, "InMemory", StringComparison.OrdinalIgnoreCase))
{
    var databaseName = builder.Configuration.GetValue<string>("Database:Name") ?? "SmartFileImport";

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase(databaseName));
}
else if (string.Equals(databaseProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));
}
else
{
    throw new InvalidOperationException(
        $"Unsupported database provider '{databaseProvider}'. Use 'SqlServer' or 'InMemory'.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        FrontendCorsPolicy,
        policy => policy
            .WithOrigins("http://localhost:5173", "https://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.Configure<FileProcessingOptions>(
    builder.Configuration.GetSection(FileProcessingOptions.SectionName));

builder.Services.AddScoped<ICsvFileReader, CsvFileReader>();
builder.Services.AddScoped<IExcelFileReader, ExcelFileReader>();
builder.Services.AddScoped<IEmployeeValidator, EmployeeValidator>();
builder.Services.AddScoped<IFileImportService, FileImportService>();
builder.Services.AddHostedService<FileImportWorker>();

builder.Services.AddControllers();

var app = builder.Build();

if (builder.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false)
    && string.Equals(databaseProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    await dbContext.Database.MigrateAsync();
}

app.UseCors(FrontendCorsPolicy);

app.MapGet("/", () => Results.Ok(new
{
    service = "Smart File Import API",
    status = "Running",
    frontend = "http://localhost:5173",
    endpoints = new
    {
        health = "/api/health",
        upload = "/api/files/upload",
        imports = "/api/imports",
        dashboard = "/api/dashboard"
    }
}));

app.MapControllers();

app.Run();
