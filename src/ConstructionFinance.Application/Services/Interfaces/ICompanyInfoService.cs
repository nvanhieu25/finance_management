using ConstructionFinance.Application.DTOs;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface ICompanyInfoService
{
    Task<CompanyInfoDto> GetAsync(CancellationToken ct = default);
    Task SaveAsync(CompanyInfoDto info, CancellationToken ct = default);
}
