using ConstructionFinance.Application.Printing;
using ConstructionFinance.Desktop;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.Tests.Finance;
using ConstructionFinance.Desktop.ViewModels.Printing;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Printing;

public class ChonMayInViewModelTests
{
    private sealed class FakeCatalog : IPrinterCatalog
    {
        public IReadOnlyList<string> InstalledPrinters { get; init; } = Array.Empty<string>();
        public string? DefaultPrinterName { get; init; }
    }

    private static ChonMayInViewModel Create(params string[] printers) =>
        new(new FakeCatalog { InstalledPrinters = printers, DefaultPrinterName = printers.FirstOrDefault() });

    [Fact]
    public void Liet_ke_moi_may_in_da_cai()
    {
        Create("Máy in A", "Máy in B").Printers.ShouldBe(new[] { "Máy in A", "Máy in B" });
    }

    [Fact]
    public void May_in_mac_dinh_duoc_chon_san()
    {
        var vm = new ChonMayInViewModel(new FakeCatalog
        {
            InstalledPrinters = new[] { "Máy in A", "Máy in B" },
            DefaultPrinterName = "Máy in B",
        });

        vm.SelectedPrinter.ShouldBe("Máy in B");
    }

    /// Máy in mặc định mà hệ điều hành báo lại không có trong danh sách (vừa bị xoá giữa hai
    /// lần đọc): chọn máy đầu danh sách, không để trống rồi bắt người dùng đoán.
    [Fact]
    public void May_in_mac_dinh_khong_co_trong_danh_sach_thi_chon_may_dau_tien()
    {
        var vm = new ChonMayInViewModel(new FakeCatalog
        {
            InstalledPrinters = new[] { "Máy in A" },
            DefaultPrinterName = "Máy in đã bị xoá",
        });

        vm.SelectedPrinter.ShouldBe("Máy in A");
    }

    [Fact]
    public void Mac_dinh_in_mot_ban()
    {
        Create("Máy in A").Copies.ShouldBe(1);
    }

    /// Review Focus 1, lần kẹp thứ hai — ở lớp giao diện. NumericUpDown có Minimum/Maximum,
    /// nhưng người dùng GÕ được chữ vào nó, và một bản dựng XAML sau này có thể bỏ sót hai
    /// thuộc tính đó. PrinterJob kẹp lần nữa ở Task 10; hai lần kẹp là có chủ ý.
    [Theory]
    [InlineData(null, 1)]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(1, 1)]
    [InlineData(99, 99)]
    [InlineData(100, 99)]
    [InlineData(100000, 99)]
    public void So_ban_bi_kep_trong_khoang_cho_phep(int? requested, int expected)
    {
        var vm = Create("Máy in A");
        vm.CopiesValue = requested;

        vm.Copies.ShouldBe(expected);
    }

    [Fact]
    public void So_ban_go_so_le_thi_lay_phan_nguyen()
    {
        var vm = Create("Máy in A");
        vm.CopiesValue = 2.7m;

        vm.Copies.ShouldBe(2);
    }

    /// Review Focus 2: máy chưa cài máy in nào. Hộp thoại không được mở ra rỗng rồi để người
    /// dùng bấm In vào hư không — CoMayIn là false và Task 12 chặn trước khi mở hộp thoại.
    [Fact]
    public void May_chua_cai_may_in_nao_thi_khong_co_gi_de_chon()
    {
        var vm = Create();

        vm.CoMayIn.ShouldBeFalse();
        vm.Printers.ShouldBeEmpty();
        vm.SelectedPrinter.ShouldBeNull();
    }

    [Fact]
    public void Nut_chinh_doc_la_In_chu_khong_phai_Luu()
    {
        var vm = Create("Máy in A");

        vm.DialogTitle.ShouldBe("Chọn máy in");
        ((IHasPrimaryButtonText)vm).PrimaryButtonText.ShouldBe("In");
    }

    /// ViewLocator tìm View theo tên (ViewModels.Printing.ChonMayInViewModel →
    /// Views.Printing.ChonMayInView). Đặt sai namespace hay sai tên tệp thì hộp thoại hiện ra
    /// chữ "View not found" mà không test nào trên view-model thấy được.
    [Fact]
    public void ViewLocator_tim_duoc_View_cua_hop_thoai()
    {
        HeadlessTestSession.Run(() =>
        {
            var control = new ViewLocator().Build(Create("Máy in A"));

            control.ShouldBeOfType<ConstructionFinance.Desktop.Views.Printing.ChonMayInView>();
        });
    }
}
