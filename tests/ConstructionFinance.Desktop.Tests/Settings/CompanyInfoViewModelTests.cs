using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Settings;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Settings;

public class CompanyInfoViewModelTests
{
    private static (CompanyInfoViewModel vm, Mock<ICompanyInfoService> svc, FakeDialogService dlg) Create(CompanyInfoDto? initial = null)
    {
        var svc = new Mock<ICompanyInfoService>(MockBehavior.Strict);
        svc.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(initial ?? new CompanyInfoDto(null, null, null, null, null, null, null, null));
        var dlg = new FakeDialogService();
        return (new CompanyInfoViewModel(svc.Object, dlg), svc, dlg);
    }

    [Fact]
    public async Task LoadAsync_populates_fields_from_service()
    {
        var dto = new CompanyInfoDto("Sở X", "123 Lê Lợi", "024-x", "i@x.vn", "/logo.png", "Mr. A", "GĐ", "Lưu VT");
        var (vm, _, _) = Create(dto);

        await vm.LoadAsync();

        vm.CompanyName.ShouldBe("Sở X");
        vm.DefaultSignerTitle.ShouldBe("GĐ");
    }

    [Fact]
    public async Task SaveCommand_calls_SaveAsync_and_shows_success_message()
    {
        var (vm, svc, dlg) = Create();
        svc.Setup(s => s.SaveAsync(It.IsAny<CompanyInfoDto>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        await vm.LoadAsync();
        vm.CompanyName = "New Org";

        await vm.SaveCommand.ExecuteAsync(null);

        svc.Verify(s => s.SaveAsync(It.Is<CompanyInfoDto>(d => d.CompanyName == "New Org"), It.IsAny<CancellationToken>()), Times.Once);
        dlg.ShownMessages.ShouldContain(m => m.Contains("lưu") || m.Contains("Đã lưu") || m.Contains("thành công"));
    }
}
