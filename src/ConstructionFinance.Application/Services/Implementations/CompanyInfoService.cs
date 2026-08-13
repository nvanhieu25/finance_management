using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;

namespace ConstructionFinance.Application.Services.Implementations;

public class CompanyInfoService : ICompanyInfoService
{
    private const string KeyCompanyName = "company_name";
    private const string KeyCompanyAddress = "company_address";
    private const string KeyCompanyPhone = "company_phone";
    private const string KeyCompanyEmail = "company_email";
    private const string KeyLogoPath = "company_logo_path";
    private const string KeySignerName = "default_signer_name";
    private const string KeySignerTitle = "default_signer_title";
    private const string KeyRecipients = "default_recipients";
    private const string KeyDonViCapTren = "don_vi_cap_tren";
    private const string KeyDonViCoSo = "don_vi_co_so";

    private readonly IAppSettingService _settings;

    public CompanyInfoService(IAppSettingService settings) => _settings = settings;

    public async Task<CompanyInfoDto> GetAsync(CancellationToken ct = default) => new(
        CompanyName: await _settings.GetAsync(KeyCompanyName, ct),
        CompanyAddress: await _settings.GetAsync(KeyCompanyAddress, ct),
        CompanyPhone: await _settings.GetAsync(KeyCompanyPhone, ct),
        CompanyEmail: await _settings.GetAsync(KeyCompanyEmail, ct),
        LogoPath: await _settings.GetAsync(KeyLogoPath, ct),
        DefaultSignerName: await _settings.GetAsync(KeySignerName, ct),
        DefaultSignerTitle: await _settings.GetAsync(KeySignerTitle, ct),
        DefaultRecipients: await _settings.GetAsync(KeyRecipients, ct),
        DonViCapTren: await _settings.GetAsync(KeyDonViCapTren, ct),
        DonViCoSo: await _settings.GetAsync(KeyDonViCoSo, ct));

    public async Task SaveAsync(CompanyInfoDto info, CancellationToken ct = default)
    {
        await _settings.SetAsync(KeyCompanyName, info.CompanyName, ct: ct);
        await _settings.SetAsync(KeyCompanyAddress, info.CompanyAddress, ct: ct);
        await _settings.SetAsync(KeyCompanyPhone, info.CompanyPhone, ct: ct);
        await _settings.SetAsync(KeyCompanyEmail, info.CompanyEmail, ct: ct);
        await _settings.SetAsync(KeyLogoPath, info.LogoPath, ct: ct);
        await _settings.SetAsync(KeySignerName, info.DefaultSignerName, ct: ct);
        await _settings.SetAsync(KeySignerTitle, info.DefaultSignerTitle, ct: ct);
        await _settings.SetAsync(KeyRecipients, info.DefaultRecipients, ct: ct);
        await _settings.SetAsync(KeyDonViCapTren, info.DonViCapTren, ct: ct);
        await _settings.SetAsync(KeyDonViCoSo, info.DonViCoSo, ct: ct);
    }
}
