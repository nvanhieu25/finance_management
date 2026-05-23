using ConstructionFinance.Domain.Common;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Entities;

public class ProjectTests
{
    [Fact]
    public void Project_is_soft_deletable_and_has_required_fields()
    {
        var project = new Project
        {
            ContractId = 1,
            ProjectCode = "DA-001",
            ProjectName = "Nhà máy XYZ",
            Status = ProjectStatus.Planning
        };

        project.ShouldBeAssignableTo<SoftDeletableEntity>();
        project.ProjectCode.ShouldBe("DA-001");
        project.Status.ShouldBe(ProjectStatus.Planning);
    }

    [Fact]
    public void Project_status_defaults_to_Planning()
    {
        var project = new Project
        {
            ContractId = 1,
            ProjectCode = "DA-001",
            ProjectName = "X"
        };

        project.Status.ShouldBe(ProjectStatus.Planning);
    }

    [Fact]
    public void Project_has_CostItems_collection_empty_by_default()
    {
        var project = new Project
        {
            ContractId = 1,
            ProjectCode = "DA-001",
            ProjectName = "X"
        };

        project.CostItems.ShouldNotBeNull();
        project.CostItems.ShouldBeEmpty();
    }
}
