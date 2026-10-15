using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using FinanceProject = ConstructionFinance.Domain.Finance.Project;
using FinanceViTri = ConstructionFinance.Domain.Finance.ViTri;
using FinanceCostGroup = ConstructionFinance.Domain.Finance.CostGroup;
using FinancePackage = ConstructionFinance.Domain.Finance.Package;
using FinanceDot = ConstructionFinance.Domain.Finance.Dot;

using FinanceFormInput = ConstructionFinance.Domain.Finance.FormInput;

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

    public DbSet<FinanceProject> FinanceProjects => Set<FinanceProject>();
    public DbSet<FinanceViTri> ViTris => Set<FinanceViTri>();
    public DbSet<FinanceCostGroup> CostGroups => Set<FinanceCostGroup>();
    public DbSet<FinancePackage> Packages => Set<FinancePackage>();
    public DbSet<FinanceDot> Dots => Set<FinanceDot>();
    public DbSet<FinanceFormInput> FormInputs => Set<FinanceFormInput>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
