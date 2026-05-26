using ConstructionFinance.Application.DTOs;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IVendorService
{
    Task<VendorDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<VendorDto>> GetAllAsync(CancellationToken ct = default);
    Task<VendorDto> CreateAsync(CreateVendorDto dto, CancellationToken ct = default);
    Task<VendorDto> UpdateAsync(UpdateVendorDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
