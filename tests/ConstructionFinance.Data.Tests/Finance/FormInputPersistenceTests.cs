using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Finance;

public class FormInputPersistenceTests
{
    private static FormInput Input(string form, string key, string? value) =>
        new() { Form = form, Key = key, Value = value };

    [Fact]
    public async Task ReplaceTreeAsync_inserts_the_inputs_and_GetTreeAsync_loads_them()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new FinanceProjectRepository(ctx);
        var id = await repo.AddAsync(new Project { Name = "DA1" });

        await repo.ReplaceTreeAsync(new Project
        {
            Id = id, Name = "DA1",
            FormInputs =
            {
                Input("thongtri", "SoThongTri", "12/TT"),
                Input("kschi", "ThueGTGT", "5000000"),
                Input("gdntt", "TaiNganHang", "Vietcombank Hải Phòng"),
            }
        });

        // GetTreeAsync là AsNoTracking; không Clear() thì identity map trả lại chính đối tượng vừa ghi.
        ctx.ChangeTracker.Clear();
        var tree = await repo.GetTreeAsync(id);

        tree!.FormInputs.Select(i => (i.Form, i.Key, i.Value)).OrderBy(t => t.Key).ShouldBe(new[]
        {
            ("thongtri", "SoThongTri", (string?)"12/TT"),
            ("gdntt", "TaiNganHang", "Vietcombank Hải Phòng"),
            ("kschi", "ThueGTGT", "5000000"),
        }.OrderBy(t => t.Item2));
    }

    [Fact]
    public async Task ReplaceTreeAsync_updates_adds_and_removes_keys()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new FinanceProjectRepository(ctx);
        var id = await repo.AddAsync(new Project { Name = "DA1" });
        await repo.ReplaceTreeAsync(new Project
        {
            Id = id, Name = "DA1",
            FormInputs = { Input("thongtri", "A", "1"), Input("thongtri", "B", "2") }
        });

        await repo.ReplaceTreeAsync(new Project
        {
            Id = id, Name = "DA1",
            FormInputs = { Input("thongtri", "B", "hai"), Input("thongtri", "C", "3") }
        });

        ctx.ChangeTracker.Clear();
        var tree = await repo.GetTreeAsync(id);
        tree!.FormInputs.ToDictionary(i => i.Key, i => i.Value)
            .ShouldBe(new Dictionary<string, string?> { ["B"] = "hai", ["C"] = "3" });
        ctx.FormInputs.Count(i => i.ProjectId == id).ShouldBe(2);
    }

    [Fact]
    public async Task The_same_key_may_exist_in_two_forms_and_two_projects()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new FinanceProjectRepository(ctx);
        var first = await repo.AddAsync(new Project { Name = "DA1" });
        var second = await repo.AddAsync(new Project { Name = "DA2" });

        await repo.ReplaceTreeAsync(new Project
        {
            Id = first, Name = "DA1",
            FormInputs = { Input("thongtri", "DonVi", "x"), Input("kschi", "DonVi", "y") }
        });
        await repo.ReplaceTreeAsync(new Project
        {
            Id = second, Name = "DA2",
            FormInputs = { Input("thongtri", "DonVi", "z") }
        });

        ctx.ChangeTracker.Clear();
        (await repo.GetTreeAsync(first))!.FormInputs.Count.ShouldBe(2);
        (await repo.GetTreeAsync(second))!.FormInputs.Single().Value.ShouldBe("z");
    }

    [Fact]
    public async Task An_explicit_empty_value_is_kept_distinct_from_a_missing_key()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new FinanceProjectRepository(ctx);
        var id = await repo.AddAsync(new Project { Name = "DA1" });

        await repo.ReplaceTreeAsync(new Project { Id = id, Name = "DA1", FormInputs = { Input("thongtri", "Khoan", "") } });

        ctx.ChangeTracker.Clear();
        (await repo.GetTreeAsync(id))!.FormInputs.Single().Value.ShouldBe("");
    }

    [Fact]
    public async Task ReplaceTreeAsync_for_a_new_project_inserts_its_inputs()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new FinanceProjectRepository(ctx);
        var project = new Project { Id = 0, Name = "Mới", FormInputs = { Input("gdntt", "So", "01") } };

        await repo.ReplaceTreeAsync(project);

        ctx.ChangeTracker.Clear();
        var tree = await repo.GetTreeAsync(project.Id);
        tree!.FormInputs.Single().Key.ShouldBe("So");
    }

    [Fact]
    public async Task Deleting_a_project_cascades_to_its_inputs()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new FinanceProjectRepository(ctx);
        var id = await repo.AddAsync(new Project { Name = "DA1" });
        await repo.ReplaceTreeAsync(new Project { Id = id, Name = "DA1", FormInputs = { Input("kschi", "A", "1") } });

        await repo.DeleteAsync(id);

        ctx.FormInputs.Count().ShouldBe(0);
    }
}
