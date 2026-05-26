using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Data;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Validators;
using FluentValidation;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class VendorServiceTests
{
    private static IVendorService CreateService(AppDbContext ctx)
    {
        var repo = new Repository<Vendor>(ctx);
        var uow = new UnitOfWork(ctx);
        IValidator<Vendor> validator = new VendorValidator();
        return new VendorService(repo, uow, validator);
    }

    [Fact]
    public async Task CreateAsync_persists_and_returns_dto_with_id()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);

        var dto = await svc.CreateAsync(new CreateVendorDto(null, "Công ty ABC", "0123456789", null, null, null, null, null));

        dto.Id.ShouldBeGreaterThan(0);
        dto.VendorName.ShouldBe("Công ty ABC");
        h.Context.Vendors.Count().ShouldBe(1);
    }

    [Fact]
    public async Task CreateAsync_throws_ValidationException_when_name_empty()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);

        await Should.ThrowAsync<ValidationException>(async () =>
            await svc.CreateAsync(new CreateVendorDto(null, "", null, null, null, null, null, null)));
    }

    [Fact]
    public async Task GetByIdAsync_throws_NotFound_when_missing()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);

        await Should.ThrowAsync<NotFoundException>(async () => await svc.GetByIdAsync(999));
    }

    [Fact]
    public async Task UpdateAsync_changes_fields()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);
        var created = await svc.CreateAsync(new CreateVendorDto(null, "Old", null, null, null, null, null, null));

        var updated = await svc.UpdateAsync(new UpdateVendorDto(created.Id, "V9", "New", null, null, null, null, null, null));

        updated.VendorName.ShouldBe("New");
        updated.VendorCode.ShouldBe("V9");
    }

    [Fact]
    public async Task DeleteAsync_soft_deletes_and_hides_from_GetAll()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);
        var created = await svc.CreateAsync(new CreateVendorDto(null, "X", null, null, null, null, null, null));

        await svc.DeleteAsync(created.Id);

        (await svc.GetAllAsync()).ShouldBeEmpty();
    }
}
