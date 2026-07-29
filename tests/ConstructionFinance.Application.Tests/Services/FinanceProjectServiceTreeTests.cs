// FinanceProjectServiceTreeTests.cs
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Services;

public class FinanceProjectServiceTreeTests
{
    private sealed class FakeRepo : IFinanceProjectRepository
    {
        private readonly Project _tree;
        public FakeRepo(Project tree) => _tree = tree;
        public Task<Project?> GetTreeAsync(int id, CancellationToken ct = default) => Task.FromResult<Project?>(_tree);
        // Remaining members throw NotImplementedException — unused by this test.
        public Task<int> AddAsync(Project p, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Project?> GetWithLinesAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Project>> ListWithLinesAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Project>> ListTreesAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(Project p, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ReplaceTreeAsync(Project p, CancellationToken ct = default) => throw new NotImplementedException();
    }

    [Fact]
    public async Task GetTreeAggregateAsync_returns_rolled_up_totals()
    {
        var tree = new Project
        {
            Name = "DA1",
            ViTris = { new ViTri { Name = "V1", CostGroups = { new CostGroup { Name = "G",
                Packages = { new Package { Name = "P",
                    Dots = { new Dot { Date = new DateOnly(2026,3,1), Fund = Fund.NSNN, PaymentKLHT = 200_000_000m, RecoveredAdvance = 5_000_000m } } } } } } } }
        };
        var svc = new FinanceProjectService(new FakeRepo(tree));

        var a = await svc.GetTreeAggregateAsync(1);

        a.ShouldNotBeNull();
        a!.DisbursedNSNN.ShouldBe(195_000_000m);
        a.SumRecovered.ShouldBe(5_000_000m);
    }
}
