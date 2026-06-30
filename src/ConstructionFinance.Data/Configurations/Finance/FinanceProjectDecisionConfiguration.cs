using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations.Finance;

public class FinanceProjectDecisionConfiguration : IEntityTypeConfiguration<ProjectDecision>
{
    public void Configure(EntityTypeBuilder<ProjectDecision> b)
    {
        b.ToTable("finance_project_decisions");
        b.HasKey(x => new { x.ProjectId, x.ApprovalDecisionId });
        b.HasOne(x => x.Decision)
            .WithMany()
            .HasForeignKey(x => x.ApprovalDecisionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
