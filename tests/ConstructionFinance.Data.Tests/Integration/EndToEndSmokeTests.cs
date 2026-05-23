using ConstructionFinance.Data.Interceptors;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Data.Seed;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Integration;

public class EndToEndSmokeTests
{
    private static AppDbContext CreateFullStack()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new TimestampInterceptor(), new AuditInterceptor())
            .Options;
        var ctx = new AppDbContext(options);
        ctx.Database.EnsureCreated();
        CostUnitTypeSeeder.Seed(ctx);
        return ctx;
    }

    [Fact]
    public async Task FullTree_QD_HD_DA_CostItems_Payments_inserts_and_queries_correctly()
    {
        using var ctx = CreateFullStack();

        var decision = new ApprovalDecision
        {
            DecisionCode = "1234/QD-UBND",
            DecisionTitle = "Phê duyệt dự án XYZ",
            IssuingAuthority = "UBND Tỉnh ABC",
            DecisionDate = new DateOnly(2026, 3, 15),
            TotalApprovedAmount = 50_000_000_000m
        };
        ctx.ApprovalDecisions.Add(decision);
        await ctx.SaveChangesAsync();
        decision.Id.ShouldBeGreaterThan(0);

        var contract = new Contract
        {
            ApprovalDecisionId = decision.Id,
            ContractCode = "HD-001",
            ContractName = "Hợp đồng xây dựng Nhà máy XYZ",
            ContractDate = new DateOnly(2026, 4, 1),
            ContractValue = 30_000_000_000m
        };
        ctx.Contracts.Add(contract);
        await ctx.SaveChangesAsync();

        var project = new Project
        {
            ContractId = contract.Id,
            ProjectCode = "DA-A",
            ProjectName = "Nhà máy XYZ giai đoạn 1",
            Status = ProjectStatus.InProgress,
            TotalBudget = 16_500_000_000m
        };
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();

        ICostRepository costRepo = new CostRepository(ctx);
        var ciXayDung = await costRepo.AddNodeAsync(project.Id, null,
            new CostItem { ProjectId = project.Id, ItemName = "Chi phí xây dựng", Path = "", Level = 0, IsLeaf = false, EstimatedAmount = 10_000_000_000m });
        await ctx.SaveChangesAsync();
        var ciPhanTho = await costRepo.AddNodeAsync(project.Id, ciXayDung.Id,
            new CostItem { ProjectId = project.Id, ItemName = "Phần thô", Path = "", Level = 0, IsLeaf = false, EstimatedAmount = 6_000_000_000m });
        await ctx.SaveChangesAsync();
        var unitM3 = ctx.CostUnitTypes.Single(u => u.Code == "m3");
        var ciMong = await costRepo.AddNodeAsync(project.Id, ciPhanTho.Id,
            new CostItem
            {
                ProjectId = project.Id,
                ItemName = "Móng cọc khoan nhồi",
                Path = "",
                Level = 0,
                IsLeaf = true,
                UnitTypeId = unitM3.Id,
                Quantity = 120m,
                UnitPrice = 25_000_000m,
                EstimatedAmount = 3_000_000_000m
            });
        await ctx.SaveChangesAsync();

        ctx.Payments.AddRange(
            new Payment { CostItemId = ciMong.Id, PaymentNo = 1, PaymentType = PaymentType.Advance, PaymentDate = new DateOnly(2026, 4, 10), Amount = 900_000_000m, Percentage = 30m, InvoiceNo = "HD-001" },
            new Payment { CostItemId = ciMong.Id, PaymentNo = 2, PaymentType = PaymentType.Installment, PaymentDate = new DateOnly(2026, 5, 15), Amount = 600_000_000m, Percentage = 20m, InvoiceNo = "HD-045" }
        );
        await ctx.SaveChangesAsync();

        ctx.ApprovalDecisions.Count().ShouldBe(1);
        ctx.Contracts.Count().ShouldBe(1);
        ctx.Projects.Count().ShouldBe(1);
        ctx.CostItems.Count().ShouldBe(3);
        ctx.Payments.Count().ShouldBe(2);

        var mong = await ctx.CostItems.AsNoTracking().FirstAsync(c => c.ItemName == "Móng cọc khoan nhồi");
        mong.Path.ShouldBe($"/{ciXayDung.Id}/{ciPhanTho.Id}/{ciMong.Id}/");
        mong.Level.ShouldBe(2);

        var subtree = await costRepo.GetSubtreeAsync(ciXayDung.Id);
        subtree.Count.ShouldBe(3);

        var totalPaid = ctx.Payments.Where(p => p.CostItemId == ciMong.Id)
            .AsEnumerable()
            .Sum(p => p.Amount);
        totalPaid.ShouldBe(1_500_000_000m);

        ctx.AuditLogs.Count(l => l.Action == AuditAction.Create).ShouldBeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task SoftDelete_decision_hides_from_default_query_but_RESTRICT_blocks_when_children_exist()
    {
        using var ctx = CreateFullStack();
        var d = new ApprovalDecision { DecisionCode = "X", DecisionTitle = "X", IssuingAuthority = "X", DecisionDate = new DateOnly(2026, 1, 1), TotalApprovedAmount = 100m };
        ctx.ApprovalDecisions.Add(d);
        await ctx.SaveChangesAsync();
        ctx.Contracts.Add(new Contract { ApprovalDecisionId = d.Id, ContractCode = "C", ContractName = "C", ContractDate = new DateOnly(2026, 1, 1), ContractValue = 100m });
        await ctx.SaveChangesAsync();

        await Should.ThrowAsync<Exception>(async () =>
        {
            ctx.ApprovalDecisions.Remove(d);
            await ctx.SaveChangesAsync();
        });

        ctx.ChangeTracker.Clear();
        var dReload = await ctx.ApprovalDecisions.FirstAsync();
        dReload.DeletedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        ctx.ApprovalDecisions.Count().ShouldBe(0);
        ctx.ApprovalDecisions.IgnoreQueryFilters().Count().ShouldBe(1);
    }
}
