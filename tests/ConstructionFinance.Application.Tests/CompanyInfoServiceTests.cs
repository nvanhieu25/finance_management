using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Data;
using ConstructionFinance.Data.Repositories;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class CompanyInfoServiceTests
{
    private static ICompanyInfoService CreateService(AppDbContext ctx)
    {
        var settings = new AppSettingService(new AppSettingRepository(ctx), new UnitOfWork(ctx));
        return new CompanyInfoService(settings);
    }

    [Fact]
    public async Task GetAsync_returns_empty_defaults_when_unset()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);

        var info = await svc.GetAsync();

        info.CompanyName.ShouldBeNullOrEmpty();
        info.LogoPath.ShouldBeNullOrEmpty();
    }

    [Fact]
    public async Task SaveAsync_then_GetAsync_round_trips_all_fields()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);

        var input = new CompanyInfoDto(
            CompanyName: "Sở Xây dựng",
            CompanyAddress: "123 Lê Lợi",
            CompanyPhone: "024-3xxx",
            CompanyEmail: "info@sxd.gov.vn",
            LogoPath: "C:/logos/sxd.png",
            DefaultSignerName: "Nguyễn Văn A",
            DefaultSignerTitle: "Giám đốc Sở",
            DefaultRecipients: "- UBND tỉnh\n- Lưu VT");
        await svc.SaveAsync(input);

        var output = await svc.GetAsync();
        output.CompanyName.ShouldBe("Sở Xây dựng");
        output.LogoPath.ShouldBe("C:/logos/sxd.png");
        output.DefaultSignerTitle.ShouldBe("Giám đốc Sở");
    }

    [Fact]
    public async Task SaveAsync_then_GetAsync_round_trips_the_two_form_org_names()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);

        await svc.SaveAsync(new CompanyInfoDto(
            "Sở X", null, null, null, null, null, null, null,
            DonViCapTren: "Sở Tài chính", DonViCoSo: "Ban QLDA số 1"));

        var stored = await svc.GetAsync();

        stored.DonViCapTren.ShouldBe("Sở Tài chính");
        stored.DonViCoSo.ShouldBe("Ban QLDA số 1");
    }
}
