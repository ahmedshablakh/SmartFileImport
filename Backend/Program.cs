using Microsoft.EntityFrameworkCore;
using SmartFileImport.Api.Configuration;
using SmartFileImport.Api.Data;
using SmartFileImport.Api.Services;
using SmartFileImport.Api.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.Configure<FileProcessingOptions>(
    builder.Configuration.GetSection(FileProcessingOptions.SectionName));

builder.Services.AddScoped<ICsvFileReader, CsvFileReader>();
builder.Services.AddScoped<IExcelFileReader, ExcelFileReader>();
builder.Services.AddScoped<IEmployeeValidator, EmployeeValidator>();
builder.Services.AddScoped<IFileImportService, FileImportService>();
builder.Services.AddHostedService<FileImportWorker>();

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();
