using Microsoft.EntityFrameworkCore;
using SmartFileImport.Api.Models;

namespace SmartFileImport.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<ImportHistory> ImportHistories => Set<ImportHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("Employees");

            entity.HasKey(employee => employee.Id);

            entity.Property(employee => employee.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(employee => employee.Email)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(employee => employee.Department)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(employee => employee.Salary)
                .HasPrecision(18, 2);

            entity.Property(employee => employee.CreatedAt)
                .IsRequired();
        });

        modelBuilder.Entity<ImportHistory>(entity =>
        {
            entity.ToTable("ImportHistories");

            entity.HasKey(importHistory => importHistory.Id);

            entity.Property(importHistory => importHistory.FileName)
                .IsRequired()
                .HasMaxLength(260);

            entity.Property(importHistory => importHistory.Status)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(importHistory => importHistory.RecordCount)
                .IsRequired();

            entity.Property(importHistory => importHistory.ProcessedAt)
                .IsRequired();

            entity.Property(importHistory => importHistory.ErrorMessage)
                .HasMaxLength(2000);
        });
    }
}
