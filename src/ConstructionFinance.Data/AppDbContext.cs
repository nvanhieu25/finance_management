using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConstructionFinance.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<CostItem> CostItems => Set<CostItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<CostUnitType> CostUnitTypes => Set<CostUnitType>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<CostTemplate> CostTemplates => Set<CostTemplate>();
    public DbSet<CostTemplateItem> CostTemplateItems => Set<CostTemplateItem>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ProjectSummary> ProjectSummaries => Set<ProjectSummary>();
    public DbSet<SettlementByYear> SettlementByYears => Set<SettlementByYear>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
