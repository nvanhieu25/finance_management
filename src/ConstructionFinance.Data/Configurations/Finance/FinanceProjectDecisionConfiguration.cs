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

        // Match ApprovalDecision's soft-delete filter: a link is only visible while its
        // decision is not soft-deleted (keeps filters consistent, resolves the EF advisory).
        b.HasQueryFilter(x => x.Decision.DeletedAt == null);
    }
}
