using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Mapping;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;
using FluentValidation;

namespace ConstructionFinance.Application.Services.Implementations;

public class VendorService : IVendorService
{
    private readonly IRepository<Vendor> _vendors;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<Vendor> _validator;

    public VendorService(IRepository<Vendor> vendors, IUnitOfWork uow, IValidator<Vendor> validator)
    {
        _vendors = vendors;
        _uow = uow;
        _validator = validator;
    }

    public async Task<VendorDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var entity = await _vendors.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Vendor), id);
        return entity.ToDto();
    }

    public async Task<IReadOnlyList<VendorDto>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _vendors.ListAsync(ct);
        return list.Select(v => v.ToDto()).ToList();
    }

    public async Task<VendorDto> CreateAsync(CreateVendorDto dto, CancellationToken ct = default)
    {
        var entity = dto.ToEntity();
        await _validator.ValidateAndThrowAsync(entity, ct);
        await _vendors.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<VendorDto> UpdateAsync(UpdateVendorDto dto, CancellationToken ct = default)
    {
        var entity = await _vendors.GetByIdAsync(dto.Id, ct) ?? throw new NotFoundException(nameof(Vendor), dto.Id);
        dto.Apply(entity);
        await _validator.ValidateAndThrowAsync(entity, ct);
        _vendors.Update(entity);
        await _uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _vendors.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Vendor), id);
        await _vendors.SoftDeleteAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
    }
}
