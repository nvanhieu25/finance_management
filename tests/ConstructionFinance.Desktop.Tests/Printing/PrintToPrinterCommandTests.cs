using ConstructionFinance.Application.Printing;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels;
using ConstructionFinance.Desktop.ViewModels.Printing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Printing;

/// Đường "In ra giấy": dựng tài liệu → hộp thoại chọn máy in → gửi lệnh in. Cổng máy in, hộp
/// thoại và danh sách máy in đều là bản giả: test không chạm máy in thật.
public class PrintToPrinterCommandTests
{
    private const string NoPrinterMessage =
        "MSG: In ra máy in — Máy này chưa cài máy in nào. Bạn có thể dùng \"Xuất Excel\" để lưu tệp rồi in từ máy khác.";

    private sealed class FakeCatalog : IPrinterCatalog
    {
        public IReadOnlyList<string> InstalledPrinters { get; init; } = new[] { "Máy in A", "Máy in B" };
        public string? DefaultPrinterName { get; init; } = "Máy in A";
    }

    private sealed class FakePrinterPort : IPrinterPort
    {
        public List<PrinterJob> Jobs { get; } = new();
        public Exception? Failure { get; set; }

        public Task PrintAsync(PrinterJob job, CancellationToken ct = default)
        {
            Jobs.Add(job);
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }

    private sealed record Harness(
        MainWindowViewModel Vm, FakeDialogService Dialog, FakePrinterPort Port);

    /// Dựng view-model với bản giả cho mọi thứ chạm thế giới ngoài. `printers` rỗng nghĩa là
    /// máy chưa cài máy in nào.
    private static Harness Create(string[]? printers = null, bool withPrinterSupport = true)
    {
        var dialog = new FakeDialogService();
        var port = new FakePrinterPort();
        var services = new ServiceCollection();
        services.AddSingleton<IDialogService>(dialog);
        services.AddSingleton<IFileLauncher>(new FakeLauncher());
        services.AddSingleton<IPrintWorkbookWriter>(new PrintWorkbookWriter());
        services.AddSingleton<IMigraDocBuilder>(new MigraDocBuilder());
        if (withPrinterSupport)
        {
            services.AddSingleton<IPrinterPort>(port);
            services.AddSingleton<IPrinterCatalog>(new FakeCatalog
            {
                InstalledPrinters = printers ?? new[] { "Máy in A", "Máy in B" },
                DefaultPrinterName = (printers ?? new[] { "Máy in A" }).FirstOrDefault(),
            });
        }

        var provider = services.BuildServiceProvider();
        // Ctor nhận HAI tham số: IServiceProvider và INavigationService.
        var vm = new MainWindowViewModel(provider, new FakeNavigationService())
        {
            CurrentContent = new FakePrintablePage(),
        };
        return new Harness(vm, dialog, port);
    }

    // ===== Đường đi thuận =====

    [Fact]
    public async Task In_sheet_nay_gui_dung_mot_lenh_in_den_may_in_da_chon()
    {
        var harness = Create();
        harness.Dialog.OnDialog = vm => ((ChonMayInViewModel)vm).SelectedPrinter = "Máy in B";

        await harness.Vm.PrintCurrentSheetCommand.ExecuteAsync(null);

        harness.Port.Jobs.Count.ShouldBe(1);
        harness.Port.Jobs[0].PrinterName.ShouldBe("Máy in B");
    }

    [Fact]
    public async Task So_ban_nguoi_dung_go_di_vao_lenh_in()
    {
        var harness = Create();
        harness.Dialog.OnDialog = vm => ((ChonMayInViewModel)vm).CopiesValue = 3m;

        await harness.Vm.PrintCurrentSheetCommand.ExecuteAsync(null);

        harness.Port.Jobs[0].Copies.ShouldBe(3);
    }

    /// Review Focus 1, lần kẹp cuối: dù hộp thoại trả về bao nhiêu, lệnh in không bao giờ
    /// mang một số bản làm tràn short.
    [Fact]
    public async Task So_ban_vo_ly_khong_bao_gio_ra_duoc_den_may_in()
    {
        var harness = Create();
        harness.Dialog.OnDialog = vm => ((ChonMayInViewModel)vm).CopiesValue = 100_000m;

        await harness.Vm.PrintCurrentSheetCommand.ExecuteAsync(null);

        harness.Port.Jobs[0].Copies.ShouldBe(PrinterJob.MaxCopies);
    }

    [Fact]
    public async Task In_toan_bo_gom_moi_sheet_vao_MOT_lenh_in()
    {
        var harness = Create();
        harness.Vm.CurrentContent = new FakePrintableWorkbook { AllSheets = { PrintFakes.Sheet("Vị trí 1"), PrintFakes.Sheet("Vị trí 2") } };

        await harness.Vm.PrintAllCommand.ExecuteAsync(null);

        harness.Port.Jobs.Count.ShouldBe(1);
        harness.Dialog.OpenedDialogs.OfType<ChonMayInViewModel>().Count().ShouldBe(1);
    }

    // ===== Người dùng hủy =====

    [Fact]
    public async Task Bam_Huy_thi_khong_in_va_khong_noi_gi()
    {
        var harness = Create();
        harness.Dialog.DialogResult = false;

        await harness.Vm.PrintCurrentSheetCommand.ExecuteAsync(null);

        harness.Port.Jobs.ShouldBeEmpty();
        harness.Dialog.ShownMessages.ShouldBeEmpty();
    }

    // ===== Máy chưa cài máy in nào =====

    /// Review Focus 2a. Không mở ra một hộp thoại rỗng: nói thẳng và gợi ý đường còn dùng được.
    [Fact]
    public async Task May_chua_cai_may_in_nao_thi_noi_thang_va_goi_y_xuat_Excel()
    {
        var harness = Create(Array.Empty<string>());

        await harness.Vm.PrintCurrentSheetCommand.ExecuteAsync(null);

        harness.Dialog.ShownMessages.ShouldBe(new[] { NoPrinterMessage });
        harness.Dialog.OpenedDialogs.ShouldBeEmpty();
        harness.Port.Jobs.ShouldBeEmpty();
    }

    /// …nhưng nút In vẫn phải sáng, vì hai mục Xuất Excel còn dùng được. Dựng cấu hình mà hai lệnh
    /// In ra giấy KHÔNG dùng được (không có cổng máy in), để chỉ còn phía Xuất Excel giữ nút sáng.
    [Fact]
    public void Hai_lenh_In_giay_mo_ma_hai_muc_Xuat_Excel_con_dung_duoc_thi_nut_In_van_sang()
    {
        var vm = Create(withPrinterSupport: false).Vm;

        vm.PrintCurrentSheetCommand.CanExecute(null).ShouldBeFalse();
        vm.CanPrint.ShouldBeTrue();
    }

    /// Hợp đồng của đích net8.0: không đăng ký IPrinterPort/IPrinterCatalog thì hai lệnh In ra giấy
    /// phải MỜ — không ném, không im lặng làm gì đó. Bản phát hành luôn là net8.0-windows; chính hợp
    /// đồng này cho phép đích net8.0 (bộ test chạy được trên máy không phải Windows) tồn tại.
    [Fact]
    public void Khong_co_cong_may_in_thi_hai_lenh_In_giay_mo_con_hai_lenh_Xuat_Excel_van_dung_duoc()
    {
        var vm = Create(withPrinterSupport: false).Vm;
        vm.CurrentContent = new FakePrintableWorkbook();

        vm.PrintCurrentSheetCommand.CanExecute(null).ShouldBeFalse();
        vm.PrintAllCommand.CanExecute(null).ShouldBeFalse();
        vm.ExportCurrentSheetCommand.CanExecute(null).ShouldBeTrue();
        vm.ExportAllCommand.CanExecute(null).ShouldBeTrue();
    }

    /// Có máy in nhưng người dùng không chọn cái nào: câu thông báo phải nói đúng việc đó,
    /// không được nói "máy này chưa cài máy in nào".
    [Fact]
    public async Task Co_may_in_nhung_khong_chon_cai_nao_thi_noi_dung_viec_do_va_khong_in()
    {
        var harness = Create();
        harness.Dialog.OnDialog = vm => ((ChonMayInViewModel)vm).SelectedPrinter = null;

        await harness.Vm.PrintCurrentSheetCommand.ExecuteAsync(null);

        var message = harness.Dialog.ShownMessages.ShouldHaveSingleItem();
        message.ShouldContain("chưa chọn máy in");
        message.ShouldNotContain("chưa cài");
        harness.Port.Jobs.ShouldBeEmpty();
    }

    // ===== Máy in báo lỗi giữa chừng =====

    /// Review Focus 2b: máy in bị xoá hoặc ngắt kết nối giữa lúc chọn và lúc in. Câu thông báo
    /// phải nêu TÊN MÁY IN và lời của hệ thống, chứ không phải một câu chung chung.
    [Fact]
    public async Task May_in_bao_loi_thi_cau_thong_bao_neu_ten_may_in_va_loi_cua_he_thong()
    {
        var harness = Create();
        harness.Port.Failure = new PrintJobFailedException(
            "Máy in A", new InvalidOperationException("Máy in không còn tồn tại."));

        await harness.Vm.PrintCurrentSheetCommand.ExecuteAsync(null);

        var message = harness.Dialog.ShownMessages.ShouldHaveSingleItem();
        message.ShouldContain("Máy in A");
        message.ShouldContain("Máy in không còn tồn tại.");
        message.ShouldContain("thử lại");
    }

    /// Lỗi máy in có lời hệ thống rỗng (ctor không cho InnerException null) thì câu không được hở ra hai dấu cách.
    [Fact]
    public async Task May_in_bao_loi_khong_kem_loi_he_thong_thi_cau_thong_bao_khong_co_hai_dau_cach()
    {
        var harness = Create();
        harness.Port.Failure = new PrintJobFailedException("Máy in A", new InvalidOperationException(""));

        await harness.Vm.PrintCurrentSheetCommand.ExecuteAsync(null);

        var message = harness.Dialog.ShownMessages.ShouldHaveSingleItem();
        message.ShouldContain("Máy in A");
        message.ShouldNotContain("  ");
    }

    /// Lỗi lạ ở bất kỳ bước nào cũng không được làm sập app: người dùng có thể đang có sổ chưa lưu.
    [Fact]
    public async Task Loi_bat_ngo_thi_bao_mot_cau_chung_chu_khong_sap_app()
    {
        var harness = Create();
        harness.Port.Failure = new OutOfMemoryException("hết bộ nhớ");

        await Should.NotThrowAsync(() => harness.Vm.PrintCurrentSheetCommand.ExecuteAsync(null));

        harness.Dialog.ShownMessages.ShouldHaveSingleItem().ShouldContain("lỗi bất ngờ");
    }

    // ===== Cảnh báo "đã bỏ qua" =====

    /// Đường ghi tệp báo "đã bỏ qua sheet nào" SAU khi ghi, trước khi mở — tệp đã nằm trên đĩa
    /// nên đọc cảnh báo rồi vẫn xử lý được. Giấy thì không rút lại được, nên ở đường in cảnh
    /// báo phải hiện TRƯỚC hộp thoại chọn máy in, để người dùng còn kịp hủy.
    [Fact]
    public async Task Canh_bao_da_bo_qua_hien_TRUOC_hop_thoai_chon_may_in()
    {
        var harness = Create();
        harness.Vm.CurrentContent = new FakePrintableWorkbook { SkipGdntt = true, AllSheets = { PrintFakes.Sheet("Vị trí 1") } };

        var order = new List<string>();
        harness.Dialog.OnMessage = _ => order.Add("cảnh báo");
        harness.Dialog.OnDialog = _ => order.Add("hộp thoại");

        await harness.Vm.PrintAllCommand.ExecuteAsync(null);

        order.ShouldBe(new[] { "cảnh báo", "hộp thoại" });
    }

    /// Và đọc xong cảnh báo mà bấm Hủy thì KHÔNG in — đó là lý do cảnh báo đứng trước.
    [Fact]
    public async Task Doc_canh_bao_roi_bam_Huy_thi_khong_in()
    {
        var harness = Create();
        harness.Vm.CurrentContent = new FakePrintableWorkbook { SkipGdntt = true, AllSheets = { PrintFakes.Sheet("Vị trí 1") } };
        harness.Dialog.DialogResult = false;

        await harness.Vm.PrintAllCommand.ExecuteAsync(null);

        // Cảnh báo phải thật sự đã hiện (và hiện trước khi hủy được), không thì "đọc rồi hủy" là lời nói suông.
        harness.Dialog.ShownMessages.ShouldHaveSingleItem().ShouldContain("GĐNTT");
        harness.Port.Jobs.ShouldBeEmpty();
    }

    /// Đổi màn thì cả bốn mục menu phải được báo bật/mờ lại. Hai mục In mới dễ bị quên ở
    /// RaisePrintStateChanged, và quên thì chúng đứng im với trạng thái của màn trước.
    [Fact]
    public void Doi_man_thi_ca_bon_lenh_duoc_bao_bat_mo_lai()
    {
        var harness = Create();
        var raised = new List<string>();
        foreach (var (name, command) in new (string, System.Windows.Input.ICommand)[]
                 {
                     ("export-sheet", harness.Vm.ExportCurrentSheetCommand),
                     ("export-all", harness.Vm.ExportAllCommand),
                     ("print-sheet", harness.Vm.PrintCurrentSheetCommand),
                     ("print-all", harness.Vm.PrintAllCommand),
                 })
        {
            var captured = name;
            command.CanExecuteChanged += (_, _) => raised.Add(captured);
        }

        harness.Vm.CurrentContent = new FakePrintableWorkbook();

        raised.ShouldBe(new[] { "export-sheet", "export-all", "print-sheet", "print-all" },
            ignoreOrder: true);
    }
}
