using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Finance;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Services;

/// FinanceProjectService chỉ chuyển tiếp cho kho dữ liệu: mọi phép tính nằm ở TreeCalculator và các builder.
public class FinanceProjectServiceTests
{
    [Fact]
    public async Task Every_member_forwards_to_the_repository()
    {
        var project = new Project { Id = 3, Name = "DA1" };
        var repo = new Mock<IFinanceProjectRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { project });
        repo.Setup(r => r.GetTreeAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        repo.Setup(r => r.ListTreesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { project });
        var service = new FinanceProjectService(repo.Object);

        (await service.ListAsync()).ShouldHaveSingleItem().ShouldBeSameAs(project);
        (await service.GetTreeAsync(3)).ShouldBeSameAs(project);
        (await service.ListTreesAsync()).ShouldHaveSingleItem().ShouldBeSameAs(project);
        await service.ReplaceTreeAsync(project);
        await service.DeleteAsync(3);

        repo.Verify(r => r.ReplaceTreeAsync(project, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.DeleteAsync(3, It.IsAny<CancellationToken>()), Times.Once);
    }
}
