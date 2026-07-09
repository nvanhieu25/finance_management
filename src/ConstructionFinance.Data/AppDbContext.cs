using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using FinanceProject = ConstructionFinance.Domain.Finance.Project;
using FinanceProjectLineItem = ConstructionFinance.Domain.Finance.ProjectLineItem;
using FinanceProjectDecision = ConstructionFinance.Domain.Finance.ProjectDecision;

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

    public DbSet<FinanceProject> FinanceProjects => Set<FinanceProject>();
    public DbSet<FinanceProjectLineItem> FinanceProjectLines => Set<FinanceProjectLineItem>();
    public DbSet<FinanceProjectDecision> FinanceProjectDecisions => Set<FinanceProjectDecision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
