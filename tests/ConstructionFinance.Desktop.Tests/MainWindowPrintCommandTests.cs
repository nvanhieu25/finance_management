using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ConstructionFinance.Application.Printing;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.Tests.Finance;
using ConstructionFinance.Desktop.ViewModels;
using ConstructionFinance.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests;

/// Nút In ở khung chung: khi nào bật/mờ, nó nghe màn hình đang mở thế nào (và có nhả ra đúng lúc không),
/// và đường chọn nơi lưu → ghi → mở tệp, kể cả mọi kiểu hỏng. Bộ ghi, hộp thoại và bộ mở tệp đều là bản giả:
/// test không chạm đĩa thật (trừ một test nối thẳng bộ ghi thật) và không mở ứng dụng nào. Phần cuối lớp chạy
/// CỬA SỔ CHÍNH THẬT (MainWindow.axaml) trên nền headless của Avalonia, vì chỗ XAML nối vào view-model là chỗ
/// không test nào trên view-model với tới được.
public class MainWindowPrintCommandTests
{
    // Những dòng người dùng đọc được, đúng từng chữ. FakeDialogService ghi mỗi thông báo thành
    // "MSG: <tiêu đề> — <nội dung>".
    private const string NothingToPrintMessage = "MSG: In — Chưa có nội dung nào để in.";
    private const string FileInUseMessage =
        "MSG: In ra Excel — Tệp đang được mở trong chương trình khác. Đóng tệp rồi thử lại.";
    private const string WriteFailedMessage =
        "MSG: In ra Excel — Không ghi được tệp in. Thử lại hoặc chọn nơi lưu khác. Chi tiết lỗi đã được ghi vào nhật ký ứng dụng.";

    private const string UnexpectedFailureMessage =
        "MSG: In ra Excel — Không in được do lỗi bất ngờ. Chi tiết lỗi đã được ghi vào nhật ký ứng dụng.";

    private const string EnabledToolTip = "In màn hình đang mở hoặc toàn bộ sổ";
    private const string DisabledToolTip = "Màn hình này không có nội dung để in";

    // ===== Bật/mờ =====

    [Fact]
    public void Nut_In_mo_khi_man_hien_tai_khong_in_duoc()
    {
        var vm = CreateMainWindowViewModel();
        vm.CurrentContent = new object();                    // màn không implement IPrintablePage

        vm.PrintCurrentSheetCommand.CanExecute(null).ShouldBeFalse();
        vm.PrintAllCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public void Nut_In_sang_khi_man_hien_tai_in_duoc()
    {
        var vm = CreateMainWindowViewModel();
        vm.CurrentContent = new FakePrintablePage("Vị trí 1");

        vm.PrintCurrentSheetCommand.CanExecute(null).ShouldBeTrue();
        vm.PrintAllCommand.CanExecute(null).ShouldBeFalse();  // không phải IPrintableWorkbook
    }

    // Hợp đồng IPrintablePage: null nghĩa là không in được. Chuỗi rỗng không phải một tên sheet.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Man_khong_co_ten_sheet_thi_In_sheet_nay_mo(string? sheetName)
    {
        var vm = CreateMainWindowViewModel();
        vm.CurrentContent = new FakePrintablePage(sheetName);

        vm.PrintCurrentSheetCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public void In_toan_bo_theo_CanPrintAll_cua_workbook()
    {
        var vm = CreateMainWindowViewModel();
        var workbook = new FakePrintableWorkbook("Vị trí 1", canPrintAll: false);
        vm.CurrentContent = workbook;

        vm.PrintAllCommand.CanExecute(null).ShouldBeFalse();   // là workbook, nhưng chưa có gì để in toàn bộ
        vm.PrintCurrentSheetCommand.CanExecute(null).ShouldBeTrue();

        workbook.SetCanPrintAll(true);

        vm.PrintAllCommand.CanExecute(null).ShouldBeTrue();
    }

    // Cả nút In sáng khi ÍT NHẤT MỘT trong hai mục dùng được, và tooltip nói đúng trạng thái.
    [Fact]
    public void Nut_In_chi_sang_khi_it_nhat_mot_muc_dung_duoc()
    {
        var vm = CreateMainWindowViewModel();

        vm.CanPrint.ShouldBeFalse();                           // chưa có màn nào
        vm.PrintToolTip.ShouldBe(DisabledToolTip);

        vm.CurrentContent = new FakePrintablePage("Vị trí 1");
        vm.CanPrint.ShouldBeTrue();
        vm.PrintToolTip.ShouldBe(EnabledToolTip);

        vm.CurrentContent = new object();                      // sáng rồi mờ lại: không phải trạng thái mặc định
        vm.CanPrint.ShouldBeFalse();
        vm.PrintToolTip.ShouldBe(DisabledToolTip);

        // Tab đang mở không in được (vd. GĐNTT chưa chọn gói thầu) nhưng tab khác thì có: chỉ còn "In toàn bộ".
        vm.CurrentContent = new FakePrintableWorkbook(sheetName: null, canPrintAll: true);
        vm.PrintCurrentSheetCommand.CanExecute(null).ShouldBeFalse();
        vm.PrintAllCommand.CanExecute(null).ShouldBeTrue();
        vm.CanPrint.ShouldBeTrue();
        vm.PrintToolTip.ShouldBe(EnabledToolTip);

        vm.CurrentContent = new FakePrintableWorkbook(sheetName: null, canPrintAll: false);
        vm.CanPrint.ShouldBeFalse();
        vm.PrintToolTip.ShouldBe(DisabledToolTip);
    }

    // CanExecute chạy rất thường xuyên: nó chỉ được ĐỌC tên, không dựng sheet, không làm mới tab nào.
    [Fact]
    public void Hoi_bat_mo_khong_dung_sheet_va_khong_lam_moi_tab()
    {
        var vm = CreateMainWindowViewModel();
        var workbook = new FakePrintableWorkbook("Vị trí 1");

        vm.CurrentContent = workbook;
        for (var i = 0; i < 3; i++)
        {
            vm.PrintCurrentSheetCommand.CanExecute(null);
            vm.PrintAllCommand.CanExecute(null);
            _ = vm.CanPrint;
            _ = vm.PrintToolTip;
        }
        workbook.SetSheetName("Vị trí 2");
        workbook.SetCanPrintAll(false);

        workbook.BuildAllCalls.ShouldBe(0);
        workbook.BuildCalls.ShouldBe(0);
    }

    // ===== Nghe màn hình đang mở =====
    //
    // Các test dưới đây ĐẾM sự kiện CanExecuteChanged thay vì chỉ gọi CanExecute: RelayCommand.CanExecute tính lại
    // điều kiện ở MỖI lần gọi nên luôn đúng dù không ai báo gì. Thứ làm nút In sáng/mờ đúng trên màn hình là sự
    // kiện — Avalonia chỉ hỏi lại lệnh khi nghe nó, không thì nút kẹt ở trạng thái cũ cho tới lần đổi tab sau.

    [Fact]
    public void Doi_man_hinh_thi_hai_lenh_va_nut_In_deu_bao_da_doi()
    {
        var vm = CreateMainWindowViewModel();
        var current = new CanExecuteChangedCounter(vm.PrintCurrentSheetCommand);
        var all = new CanExecuteChangedCounter(vm.PrintAllCommand);
        var changed = Collect(vm);

        vm.CurrentContent = new FakePrintablePage("Vị trí 1");

        current.Count.ShouldBeGreaterThan(0);
        all.Count.ShouldBeGreaterThan(0);
        changed.ShouldContain(nameof(MainWindowViewModel.CanPrint));
        changed.ShouldContain(nameof(MainWindowViewModel.PrintToolTip));
    }

    [Fact]
    public void PrintSheetName_doi_thi_trang_thai_nut_cap_nhat_lai()
    {
        var vm = CreateMainWindowViewModel();
        var page = new FakePrintablePage(null);
        vm.CurrentContent = page;
        vm.PrintCurrentSheetCommand.CanExecute(null).ShouldBeFalse();
        var current = new CanExecuteChangedCounter(vm.PrintCurrentSheetCommand);   // chỉ đếm phần do SetSheetName gây ra
        var all = new CanExecuteChangedCounter(vm.PrintAllCommand);
        var changed = Collect(vm);

        page.SetSheetName("Vị trí 2");                        // bắn PropertyChanged

        vm.PrintCurrentSheetCommand.CanExecute(null).ShouldBeTrue();
        current.Count.ShouldBeGreaterThan(0);
        all.Count.ShouldBeGreaterThan(0);
        changed.ShouldContain(nameof(MainWindowViewModel.CanPrint));
        changed.ShouldContain(nameof(MainWindowViewModel.PrintToolTip));
    }

    [Fact]
    public void CanPrintAll_doi_thi_In_toan_bo_va_nut_In_bao_da_doi()
    {
        var vm = CreateMainWindowViewModel();
        var workbook = new FakePrintableWorkbook("Vị trí 1", canPrintAll: false);
        vm.CurrentContent = workbook;
        var all = new CanExecuteChangedCounter(vm.PrintAllCommand);
        var changed = Collect(vm);

        workbook.SetCanPrintAll(true);                        // chỉ CanPrintAll đổi, PrintSheetName giữ nguyên

        vm.PrintAllCommand.CanExecute(null).ShouldBeTrue();
        all.Count.ShouldBeGreaterThan(0);
        changed.ShouldContain(nameof(MainWindowViewModel.CanPrint));
    }

    // Màn thật bắn PropertyChanged cho mọi thuộc tính (IsDirty mỗi lần gõ phím...): nút In chỉ quan tâm hai cái tên.
    [Fact]
    public void Thuoc_tinh_khac_cua_man_doi_thi_nut_In_khong_bi_hoi_lai()
    {
        var vm = CreateMainWindowViewModel();
        var page = new FakePrintablePage("Vị trí 1");
        vm.CurrentContent = page;
        var current = new CanExecuteChangedCounter(vm.PrintCurrentSheetCommand);
        var all = new CanExecuteChangedCounter(vm.PrintAllCommand);
        var changed = Collect(vm);

        page.RaiseOther("IsDirty");

        current.Count.ShouldBe(0);
        all.Count.ShouldBe(0);
        changed.ShouldBeEmpty();
    }

    // ===== Gắn và gỡ handler đi đôi với nhau =====

    // Mỗi đường đổi màn: null→A, A→B, B→null, null→A lần nữa, A→màn không lắng nghe được. Sau mỗi bước, số handler
    // còn nằm trên từng màn phải đúng bằng số màn đó đang là màn hiện tại (1 hoặc 0), không bao giờ 2.
    [Fact]
    public void Gan_va_go_handler_di_doi_nhau_o_moi_duong_doi_man()
    {
        var vm = CreateMainWindowViewModel();
        var a = new FakePrintablePage("A");
        var b = new FakePrintablePage("B");

        vm.CurrentContent = a;
        (a.SubscriberCount, b.SubscriberCount).ShouldBe((1, 0));

        vm.CurrentContent = b;
        (a.SubscriberCount, b.SubscriberCount).ShouldBe((0, 1));

        vm.CurrentContent = null;
        (a.SubscriberCount, b.SubscriberCount).ShouldBe((0, 0));

        vm.CurrentContent = a;
        (a.SubscriberCount, b.SubscriberCount).ShouldBe((1, 0));

        vm.CurrentContent = new object();
        (a.SubscriberCount, b.SubscriberCount).ShouldBe((0, 0));
    }

    [Fact]
    public void Man_da_roi_doi_PrintSheetName_thi_khong_dong_den_nut_In()
    {
        var vm = CreateMainWindowViewModel();
        var left = new FakePrintablePage("Vị trí 1");
        vm.CurrentContent = left;
        vm.CurrentContent = new FakePrintablePage("Vị trí 2");
        var current = new CanExecuteChangedCounter(vm.PrintCurrentSheetCommand);
        var all = new CanExecuteChangedCounter(vm.PrintAllCommand);
        var changed = Collect(vm);

        left.SetSheetName("Vị trí 1 (đã đổi tên)");

        current.Count.ShouldBe(0);
        all.Count.ShouldBe(0);
        changed.ShouldBeEmpty();
    }

    // ===== In sheet này =====

    [Fact]
    public async Task In_sheet_nay_ghi_tep_roi_mo_bang_ung_dung_mac_dinh()
    {
        var writer = new FakeWriter();
        var launcher = new FakeLauncher();
        var dialog = new FakeDialogService();
        var vm = CreateMainWindowViewModel(writer, launcher, dialog, savePath: "/tmp/x.xlsx");
        var page = new FakePrintablePage("Vị trí 1");
        vm.CurrentContent = page;

        await vm.PrintCurrentSheetCommand.ExecuteAsync(null);

        writer.WrittenSheets.Count.ShouldBe(1);
        writer.WrittenSheets[0].ShouldBeSameAs(page.BuiltSheet);   // đúng sheet của màn, không phải một bản trùng giá trị
        writer.WrittenPath.ShouldBe("/tmp/x.xlsx");
        launcher.Opened.ShouldBe("/tmp/x.xlsx");
        dialog.ShownMessages.ShouldBeEmpty();                      // mở được thì không báo gì thêm
    }

    [Fact]
    public async Task Nguoi_dung_huy_hop_thoai_thi_khong_ghi_gi()
    {
        var writer = new FakeWriter();
        var launcher = new FakeLauncher();
        var dialog = new FakeDialogService();
        var vm = CreateMainWindowViewModel(writer, launcher, dialog, savePath: null);   // null = bấm Hủy
        vm.CurrentContent = new FakePrintablePage("Vị trí 1");

        await vm.PrintCurrentSheetCommand.ExecuteAsync(null);

        writer.WrittenSheets.ShouldBeEmpty();
        writer.WriteCalls.ShouldBe(0);
        launcher.Opened.ShouldBeNull();
        dialog.ShownMessages.ShouldBeEmpty();                      // Hủy là im lặng, không báo lỗi
    }

    [Fact]
    public async Task Hop_thoai_chon_noi_luu_nhan_ten_goi_y_va_duoi_xlsx()
    {
        var dialog = new FakeDialogService();
        var vm = CreateMainWindowViewModel(dialog: dialog, savePath: "/tmp/x.xlsx");
        vm.CurrentContent = new FakePrintableWorkbook("Vị trí 1") { PrintWorkbookName = "DA-01" };

        await vm.PrintCurrentSheetCommand.ExecuteAsync(null);

        var request = dialog.SaveFileRequests.ShouldHaveSingleItem();
        request.Title.ShouldBe("In ra Excel");
        request.Extensions.ShouldBe(new[] { ".xlsx" });
        // <Mã dự án>-<Tên sheet bỏ dấu>-<yyyyMMdd-HHmm>.xlsx; thời điểm là giờ máy nên chỉ kiểm hình dạng.
        request.DefaultFileName.ShouldMatch(@"^DA-01-Vi tri 1-\d{8}-\d{4}\.xlsx$");
    }

    [Fact]
    public async Task Chan_trang_ghi_ten_du_an_va_ten_Vi_tri_khi_in_mot_sheet()
    {
        var writer = new FakeWriter();
        var vm = CreateMainWindowViewModel(writer, savePath: "/tmp/x.xlsx");
        vm.CurrentContent = new FakePrintableWorkbook("Vị trí 1") { PrintWorkbookName = "DA-01" };

        await vm.PrintCurrentSheetCommand.ExecuteAsync(null);

        writer.WrittenFooter.ShouldBe(new PrintFooterInfo("DA-01", "Vị trí 1"));
    }

    [Fact]
    public async Task In_sheet_nay_ma_man_khong_tra_sheet_thi_bao_chu_khong_im_lang()
    {
        var writer = new FakeWriter();
        var dialog = new FakeDialogService();
        var vm = CreateMainWindowViewModel(writer, dialog: dialog, savePath: "/tmp/x.xlsx");
        // Vi phạm hợp đồng IPrintablePage (có tên mà không có sheet): không nên xảy ra, nhưng nút vừa được bấm
        // thì không được im lặng.
        vm.CurrentContent = new FakePrintablePage("Vị trí 1") { ReturnsNoSheet = true };

        await vm.PrintCurrentSheetCommand.ExecuteAsync(null);

        dialog.ShownMessages.ShouldBe(new[] { NothingToPrintMessage });
        dialog.SaveFileRequests.ShouldBeEmpty();
        writer.WriteCalls.ShouldBe(0);
    }

    // ===== In toàn bộ =====

    [Fact]
    public async Task In_toan_bo_ghi_moi_sheet_theo_thu_tu_va_dat_ten_tep_Toan_bo()
    {
        var writer = new FakeWriter();
        var launcher = new FakeLauncher();
        var dialog = new FakeDialogService();
        var vm = CreateMainWindowViewModel(writer, launcher, dialog, savePath: "/tmp/all.xlsx");
        var workbook = new FakePrintableWorkbook("Vị trí 1") { PrintWorkbookName = "DA-01" };
        var first = Sheet("Tổng hợp");
        var second = Sheet("Vị trí 1");
        var third = Sheet("Quyết toán năm");
        workbook.AllSheets.AddRange(new[] { first, second, third });
        vm.CurrentContent = workbook;

        await vm.PrintAllCommand.ExecuteAsync(null);

        writer.WrittenSheets.Count.ShouldBe(3);
        writer.WrittenSheets[0].ShouldBeSameAs(first);
        writer.WrittenSheets[1].ShouldBeSameAs(second);
        writer.WrittenSheets[2].ShouldBeSameAs(third);
        writer.WrittenPath.ShouldBe("/tmp/all.xlsx");
        launcher.Opened.ShouldBe("/tmp/all.xlsx");
        workbook.BuildAllCalls.ShouldBe(1);                       // dựng một lần mỗi lần in thật
        dialog.SaveFileRequests.ShouldHaveSingleItem().DefaultFileName
            .ShouldMatch(@"^DA-01-Toan bo-\d{8}-\d{4}\.xlsx$");
    }

    // In cả sổ thì chân trang chỉ có tên dự án: không có Vị trí nào để nêu tên.
    [Fact]
    public async Task Chan_trang_in_toan_bo_khong_co_ten_Vi_tri()
    {
        var writer = new FakeWriter();
        var vm = CreateMainWindowViewModel(writer, savePath: "/tmp/all.xlsx");
        var workbook = new FakePrintableWorkbook("Vị trí 1") { PrintWorkbookName = "DA-01" };
        workbook.AllSheets.AddRange(new[] { Sheet("Tổng hợp"), Sheet("Vị trí 1") });
        vm.CurrentContent = workbook;

        await vm.PrintAllCommand.ExecuteAsync(null);

        writer.WrittenFooter.ShouldBe(new PrintFooterInfo("DA-01", null));
    }

    // Chỉ còn đúng một sheet in được thì "In toàn bộ" vẫn là in cả sổ: nhãn tệp "Toan bo" không phải tên một Vị trí
    // nên không được lọt vào chân trang như thể nó là tên Vị trí.
    [Fact]
    public async Task In_toan_bo_chi_co_mot_sheet_van_khong_ghi_nhan_Toan_bo_vao_chan_trang()
    {
        var writer = new FakeWriter();
        var vm = CreateMainWindowViewModel(writer, savePath: "/tmp/all.xlsx");
        var workbook = new FakePrintableWorkbook("Vị trí 1") { PrintWorkbookName = "DA-01" };
        workbook.AllSheets.Add(Sheet("Tổng hợp"));
        vm.CurrentContent = workbook;

        await vm.PrintAllCommand.ExecuteAsync(null);

        writer.WrittenSheets.Count.ShouldBe(1);
        writer.WrittenFooter.ShouldBe(new PrintFooterInfo("DA-01", null));
    }

    [Fact]
    public async Task In_toan_bo_ma_khong_co_sheet_nao_thi_bao_chu_khong_im_lang()
    {
        var writer = new FakeWriter();
        var dialog = new FakeDialogService();
        var vm = CreateMainWindowViewModel(writer, dialog: dialog, savePath: "/tmp/all.xlsx");
        vm.CurrentContent = new FakePrintableWorkbook("Vị trí 1");   // CanPrintAll đúng, nhưng BuildAllSheets rỗng

        await vm.PrintAllCommand.ExecuteAsync(null);

        dialog.ShownMessages.ShouldBe(new[] { NothingToPrintMessage });
        dialog.SaveFileRequests.ShouldBeEmpty();                   // chưa có gì để in thì không hỏi nơi lưu
        writer.WriteCalls.ShouldBe(0);
    }

    // ===== Khi ghi/mở tệp không thành =====

    [Fact]
    public async Task Tep_dang_mo_trong_chuong_trinh_khac_thi_bao_dung_cau_va_khong_mo_tep()
    {
        var writer = new FakeWriter { FailWith = new IOException("The process cannot access the file") };
        var launcher = new FakeLauncher();
        var dialog = new FakeDialogService();
        var vm = CreateMainWindowViewModel(writer, launcher, dialog, savePath: "/tmp/x.xlsx");
        vm.CurrentContent = new FakePrintablePage("Vị trí 1");

        await vm.PrintCurrentSheetCommand.ExecuteAsync(null);

        dialog.ShownMessages.ShouldBe(new[] { FileInUseMessage });
        launcher.Opened.ShouldBeNull();
    }

    // Lỗi ngoài IOException (không có quyền ghi, tên sheet/chân trang mang surrogate đứt đôi làm ClosedXML ném
    // ArgumentException...) cũng không được làm app sập hay im lặng: báo chung, ghi nhật ký, không mở tệp.
    [Theory]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task Loi_khac_khi_ghi_thi_bao_chung_va_khong_mo_tep(Type exceptionType)
    {
        var failure = (Exception)Activator.CreateInstance(exceptionType, "boom")!;
        var writer = new FakeWriter { FailWith = failure };
        var launcher = new FakeLauncher();
        var dialog = new FakeDialogService();
        var vm = CreateMainWindowViewModel(writer, launcher, dialog, savePath: "/tmp/x.xlsx");
        vm.CurrentContent = new FakePrintablePage("Vị trí 1");

        await vm.PrintCurrentSheetCommand.ExecuteAsync(null);      // không ném ra ngoài

        dialog.ShownMessages.ShouldBe(new[] { WriteFailedMessage });   // câu chung: không lộ chữ của ngoại lệ ("boom")
        launcher.Opened.ShouldBeNull();
    }

    [Fact]
    public async Task Loi_khac_khi_ghi_duoc_ghi_vao_nhat_ky_kem_ngoai_le()
    {
        var failure = new UnauthorizedAccessException("boom");
        var writer = new FakeWriter { FailWith = failure };
        var vm = CreateMainWindowViewModel(writer, savePath: "/tmp/x.xlsx");
        vm.CurrentContent = new FakePrintablePage("Vị trí 1");

        var logged = await CaptureLogAsync(() => vm.PrintCurrentSheetCommand.ExecuteAsync(null));

        // Lọc theo chính đối tượng ngoại lệ: Serilog.Log là toàn cục nên có thể lẫn dòng của test khác.
        var entry = logged.Where(e => ReferenceEquals(e.Exception, failure)).ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogEventLevel.Error);
    }

    [Fact]
    public async Task Khong_mo_duoc_tep_thi_hien_duong_dan_de_nguoi_dung_tu_mo()
    {
        var launcher = new FakeLauncher { Result = false };
        var dialog = new FakeDialogService();
        var vm = CreateMainWindowViewModel(launcher: launcher, dialog: dialog, savePath: "/tmp/x.xlsx");
        vm.CurrentContent = new FakePrintablePage("Vị trí 1");

        await vm.PrintCurrentSheetCommand.ExecuteAsync(null);

        launcher.Opened.ShouldBe("/tmp/x.xlsx");
        dialog.ShownMessages.ShouldBe(new[] { "MSG: In ra Excel — Đã lưu tệp tại:\n/tmp/x.xlsx" });
    }

    // Nối với bộ ghi THẬT: lời hứa "lỗi ClosedXML ném vì surrogate đứt đôi sẽ do lệnh In bắt" chỉ đúng nếu bộ ghi thật
    // quả có ném và lệnh quả có bắt. Bộ ghi dựng cả workbook trong bộ nhớ rồi mới chạm đĩa nên tệp đích không được tạo.
    [Fact]
    public async Task Surrogate_dut_doi_trong_ten_sheet_lam_bo_ghi_that_nem_va_lenh_In_van_bao_loi()
    {
        var path = Path.Combine(Path.GetTempPath(), $"in-test-{Guid.NewGuid():N}.xlsx");
        try
        {
            var launcher = new FakeLauncher();
            var dialog = new FakeDialogService();
            var vm = CreateMainWindowViewModel(new PrintWorkbookWriter(), launcher, dialog, savePath: path);
            vm.CurrentContent = new FakePrintablePage("Vị trí " + (char)0xD800);   // nửa đầu của một cặp surrogate, lẻ loi

            await vm.PrintCurrentSheetCommand.ExecuteAsync(null);

            dialog.ShownMessages.ShouldBe(new[] { WriteFailedMessage });
            launcher.Opened.ShouldBeNull();
            File.Exists(path).ShouldBeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ===== Lỗi ngoài bước ghi tệp =====
    //
    // CommunityToolkit ném lại MỌI lỗi lọt ra khỏi một AsyncRelayCommand lên luồng giao diện (mặc định của nó, đã đo bằng
    // một SynchronizationContext bắt lỗi), tức là làm sập app trong lúc người dùng có thể đang có sổ chưa lưu. Nên lỗi ở
    // BẤT KỲ bước nào — dựng sheet của màn, hộp thoại chọn nơi lưu... chứ không riêng bước ghi — phải dừng trong lệnh.

    [Fact]
    public async Task Man_nem_loi_khi_dung_sheet_thi_In_sheet_nay_bao_chung_va_ghi_nhat_ky()
    {
        var failure = new InvalidOperationException("boom");
        var writer = new FakeWriter();
        var dialog = new FakeDialogService();
        var vm = CreateMainWindowViewModel(writer, dialog: dialog, savePath: "/tmp/x.xlsx");
        vm.CurrentContent = new FakePrintablePage("Vị trí 1") { BuildFailure = failure };

        var logged = await CaptureLogAsync(() => vm.PrintCurrentSheetCommand.ExecuteAsync(null));   // không ném ra ngoài

        dialog.ShownMessages.ShouldBe(new[] { UnexpectedFailureMessage });
        dialog.SaveFileRequests.ShouldBeEmpty();
        writer.WriteCalls.ShouldBe(0);
        logged.Where(e => ReferenceEquals(e.Exception, failure)).ShouldHaveSingleItem()
            .Level.ShouldBe(LogEventLevel.Error);
    }

    [Fact]
    public async Task Workbook_nem_loi_khi_gom_sheet_thi_In_toan_bo_bao_chung_va_ghi_nhat_ky()
    {
        var failure = new InvalidOperationException("boom");
        var writer = new FakeWriter();
        var dialog = new FakeDialogService();
        var vm = CreateMainWindowViewModel(writer, dialog: dialog, savePath: "/tmp/all.xlsx");
        vm.CurrentContent = new FakePrintableWorkbook("Vị trí 1") { BuildAllFailure = failure };

        var logged = await CaptureLogAsync(() => vm.PrintAllCommand.ExecuteAsync(null));

        dialog.ShownMessages.ShouldBe(new[] { UnexpectedFailureMessage });
        dialog.SaveFileRequests.ShouldBeEmpty();
        writer.WriteCalls.ShouldBe(0);
        logged.Where(e => ReferenceEquals(e.Exception, failure)).ShouldHaveSingleItem()
            .Level.ShouldBe(LogEventLevel.Error);
    }

    // Lỗi ở một bước TRONG PrintAsync nhưng trước bước ghi: cho thấy vỏ bọc cả đường đi chứ không chỉ mấy dòng đầu của lệnh.
    [Fact]
    public async Task Hop_thoai_chon_noi_luu_nem_loi_thi_bao_chung_chu_khong_ghi_hay_mo_tep()
    {
        var writer = new FakeWriter();
        var launcher = new FakeLauncher();
        var dialog = new FakeDialogService { PickSaveFileFailure = new InvalidOperationException("picker down") };
        var vm = CreateMainWindowViewModel(writer, launcher, dialog, savePath: "/tmp/x.xlsx");
        vm.CurrentContent = new FakePrintablePage("Vị trí 1");

        await vm.PrintCurrentSheetCommand.ExecuteAsync(null);

        dialog.ShownMessages.ShouldBe(new[] { UnexpectedFailureMessage });
        writer.WriteCalls.ShouldBe(0);
        launcher.Opened.ShouldBeNull();
    }

    // ===== Nút In trên cửa sổ chính thật =====
    //
    // Chạy MainWindow.axaml thật với một MainWindowViewModel thật. Hai thứ test view-model không với tới: biểu thức
    // binding trong XAML (gõ sai tên là lỗi biên dịch, nhưng gắn nhầm lệnh vào nhầm mục thì không), và việc DataContext
    // có đi được vào MenuFlyout hay không — MenuItem mà không có Command thì VẪN sáng, bấm vào chẳng có gì xảy ra.

    [Fact]
    public void Nut_In_tren_cua_so_that_mo_di_khi_man_khong_in_duoc_va_sang_lai_khi_in_duoc()
    {
        RunWithMainWindow(BuildServices(null, null, null, savePath: null), (vm, window, button) =>
        {
            button.IsEnabled.ShouldBeFalse();                          // chưa có màn nào
            ToolTip.GetTip(button).ShouldBe(DisabledToolTip);
            ToolTip.GetShowOnDisabled(button).ShouldBeTrue();          // nút mờ vẫn cho đọc vì sao

            vm.CurrentContent = new FakePrintablePage("Vị trí 1");
            button.IsEnabled.ShouldBeTrue();
            ToolTip.GetTip(button).ShouldBe(EnabledToolTip);

            // Sáng thì bấm mở menu. Bước này cũng chứng minh cú bấm của test trúng nút, để bước mờ bên dưới không đúng
            // chỉ vì bấm trượt.
            Click(window, button);
            button.Flyout!.IsOpen.ShouldBeTrue();
            button.Flyout.Hide();

            vm.CurrentContent = new object();
            button.IsEnabled.ShouldBeFalse();
            ToolTip.GetTip(button).ShouldBe(DisabledToolTip);

            Click(window, button);
            button.Flyout.IsOpen.ShouldBeFalse();                      // mờ thì bấm không mở menu
        });
    }

    [Fact]
    public void Hai_muc_menu_In_gan_dung_hai_lenh_va_mo_theo_tung_lenh()
    {
        RunWithMainWindow(BuildServices(null, null, null, savePath: null), (vm, window, button) =>
        {
            vm.CurrentContent = new FakePrintableWorkbook("Vị trí 1");   // in được cả sheet này lẫn toàn bộ

            Click(window, button);

            var flyout = (MenuFlyout)button.Flyout!;
            flyout.IsOpen.ShouldBeTrue();
            var items = flyout.Items.OfType<MenuItem>().ToList();
            items.Count.ShouldBe(2);
            var currentItem = items.Single(i => (string?)i.Header == "In sheet này");
            var allItem = items.Single(i => (string?)i.Header == "In toàn bộ");

            // Đúng lệnh ở đúng mục. Command null nghĩa là DataContext không đi vào được MenuFlyout.
            currentItem.Command.ShouldBeSameAs(vm.PrintCurrentSheetCommand);
            allItem.Command.ShouldBeSameAs(vm.PrintAllCommand);
            currentItem.IsEffectivelyEnabled.ShouldBeTrue();
            allItem.IsEffectivelyEnabled.ShouldBeTrue();

            // Menu còn mở mà màn đổi sang màn chỉ in được từng sheet: "In toàn bộ" mờ đi ngay, không cần mở lại.
            vm.CurrentContent = new FakePrintablePage("Vị trí 2");
            currentItem.IsEffectivelyEnabled.ShouldBeTrue();
            allItem.IsEffectivelyEnabled.ShouldBeFalse();
        });
    }

    [Fact]
    public void Bam_In_sheet_nay_tren_menu_ghi_tep_roi_mo_tep()
    {
        var writer = new FakeWriter();
        var launcher = new FakeLauncher();
        var services = BuildServices(writer, launcher, dialog: null, savePath: "/tmp/x.xlsx");

        RunWithMainWindow(services, (vm, window, button) =>
        {
            vm.CurrentContent = new FakePrintableWorkbook("Vị trí 1");
            Click(window, button);
            var item = ((MenuFlyout)button.Flyout!).Items.OfType<MenuItem>()
                .Single(i => (string?)i.Header == "In sheet này");

            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));   // như người dùng bấm vào mục menu
            Dispatcher.UIThread.RunJobs();

            writer.WrittenSheets.Count.ShouldBe(1);
            launcher.Opened.ShouldBe("/tmp/x.xlsx");
        });
    }

    // ===== Dụng cụ =====

    /// `savePath` là thứ hộp thoại chọn nơi lưu trả về; null nghĩa là người dùng bấm Hủy.
    private static MainWindowViewModel CreateMainWindowViewModel(
        IPrintWorkbookWriter? writer = null,
        FakeLauncher? launcher = null,
        FakeDialogService? dialog = null,
        string? savePath = null)
        => new(BuildServices(writer, launcher, dialog, savePath), new FakeNavigationService());

    private static IServiceProvider BuildServices(
        IPrintWorkbookWriter? writer, FakeLauncher? launcher, FakeDialogService? dialog, string? savePath)
    {
        dialog ??= new FakeDialogService();
        dialog.PickSaveFileResult = savePath;

        var services = new ServiceCollection();
        services.AddSingleton<IDialogService>(dialog);
        services.AddSingleton<IPrintWorkbookWriter>(writer ?? new FakeWriter());
        services.AddSingleton<IFileLauncher>(launcher ?? new FakeLauncher());
        return services.BuildServiceProvider();
    }

    private static PrintSheet Sheet(string name) => new PrintTableSheet(
        name,
        PrintPageProfiles.ViTriLedger,
        new[] { new PrintColumn("Cột", 10, PrintAlign.Left) },
        Array.Empty<PrintRow>());

    private static List<string?> Collect(INotifyPropertyChanged source)
    {
        var names = new List<string?>();
        source.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        return names;
    }

    /// Chạy `action` với Serilog.Log trỏ vào một bể chứa riêng, trả về các dòng nhật ký bắt được, rồi trả Log về như cũ.
    private static async Task<IReadOnlyList<LogEvent>> CaptureLogAsync(Func<Task> action)
    {
        var sink = new CapturingSink();
        var previous = Log.Logger;
        using var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        Log.Logger = logger;
        try
        {
            await action();
        }
        finally
        {
            Log.Logger = previous;
        }
        return sink.Events;
    }

    /// Dựng cửa sổ chính THẬT trong phiên headless dùng chung (xem Finance/HeadlessTestSupport.cs) và đưa cho `body`
    /// view-model, cửa sổ và nút In. MainWindow lấy IDialogService từ App.Host ngay khi dựng, mà App.Host chỉ được đặt
    /// trong App.OnFrameworkInitializationCompleted (bản test cố ý bỏ qua nó vì nó chạy migration lên DB thật của người
    /// dùng), nên đặt tạm một Host giả bọc `services` rồi trả về như cũ khi xong. Chỉ MainWindow đọc App.Host.
    private static void RunWithMainWindow(IServiceProvider services, Action<MainWindowViewModel, Window, Button> body)
    {
        HeadlessTestSession.Run(() =>
        {
            SetAppHost(new FakeHost(services));
            try
            {
                var vm = new MainWindowViewModel(services, new FakeNavigationService());
                var window = new MainWindow { DataContext = vm };
                try
                {
                    window.Show();
                    Dispatcher.UIThread.RunJobs();
                    var button = window.FindControl<Button>("PrintButton")
                        ?? throw new InvalidOperationException("MainWindow.axaml không còn nút có x:Name=\"PrintButton\".");
                    body(vm, window, button);
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                SetAppHost(null);
            }
        });
    }

    private static void SetAppHost(IHost? host) =>
        typeof(App).GetProperty(nameof(App.Host))!.SetValue(null, host);

    /// Một cú bấm chuột thật (nhấn rồi nhả) vào giữa `control` qua hệ thống nhập của cửa sổ — như
    /// VitriLedgerViewTests.Click.
    private static void Click(Window window, Control control)
    {
        var center = new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
        var point = control.TranslatePoint(center, window)
            ?? throw new InvalidOperationException("Control không nằm trong cây hiển thị của cửa sổ.");
        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class FakeHost : IHost
    {
        public FakeHost(IServiceProvider services) => Services = services;

        public IServiceProvider Services { get; }

        public void Dispose()
        {
        }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class CapturingSink : ILogEventSink
    {
        private readonly List<LogEvent> _events = new();

        public IReadOnlyList<LogEvent> Events
        {
            get { lock (_events) return _events.ToList(); }
        }

        public void Emit(LogEvent logEvent)
        {
            lock (_events) _events.Add(logEvent);
        }
    }

    /// Đếm số lần một lệnh báo CanExecuteChanged.
    private sealed class CanExecuteChangedCounter
    {
        public CanExecuteChangedCounter(ICommand command) => command.CanExecuteChanged += (_, _) => Count++;

        public int Count { get; private set; }
    }

    /// Màn in được. PrintSheetName đổi được lúc chạy và bắn PropertyChanged như màn thật.
    private class FakePrintablePage : IPrintablePage, INotifyPropertyChanged
    {
        private string? _sheetName;

        public FakePrintablePage(string? sheetName) => _sheetName = sheetName;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string? PrintSheetName => _sheetName;

        /// Mô phỏng một màn vi phạm hợp đồng IPrintablePage: có tên mà không trả được sheet.
        public bool ReturnsNoSheet { get; init; }

        /// Sheet mà lần BuildPrintSheet gần nhất đã trả (null nếu chưa gọi lần nào).
        public PrintSheet? BuiltSheet { get; private set; }

        public int BuildCalls { get; private set; }

        /// Khác null thì BuildPrintSheet ném: mô phỏng lỗi trong code của chính màn.
        public Exception? BuildFailure { get; init; }

        public PrintSheet? BuildPrintSheet()
        {
            BuildCalls++;
            if (BuildFailure is not null) throw BuildFailure;
            if (ReturnsNoSheet || _sheetName is null) return null;
            return BuiltSheet = Sheet(_sheetName);
        }

        public void SetSheetName(string? sheetName)
        {
            _sheetName = sheetName;
            Raise(nameof(PrintSheetName));
        }

        public void RaiseOther(string propertyName) => Raise(propertyName);

        /// Số handler đang nghe màn này: chứng cứ trực tiếp cho việc gắn/gỡ có đi đôi hay không.
        public int SubscriberCount => PropertyChanged?.GetInvocationList().Length ?? 0;

        protected void Raise(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class FakePrintableWorkbook : FakePrintablePage, IPrintableWorkbook
    {
        private bool _canPrintAll;

        public FakePrintableWorkbook(string? sheetName, bool canPrintAll = true) : base(sheetName) =>
            _canPrintAll = canPrintAll;

        public string PrintWorkbookName { get; init; } = "DA-01";

        public bool CanPrintAll => _canPrintAll;

        public List<PrintSheet> AllSheets { get; } = new();

        public int BuildAllCalls { get; private set; }

        /// Khác null thì BuildAllSheets ném (vd. làm mới một tab tổng hợp hỏng).
        public Exception? BuildAllFailure { get; init; }

        public IReadOnlyList<PrintSheet> BuildAllSheets()
        {
            BuildAllCalls++;
            if (BuildAllFailure is not null) throw BuildAllFailure;
            return AllSheets;
        }

        public void SetCanPrintAll(bool value)
        {
            _canPrintAll = value;
            Raise(nameof(CanPrintAll));
        }
    }

    private sealed class FakeWriter : IPrintWorkbookWriter
    {
        public List<PrintSheet> WrittenSheets { get; } = new();

        public string? WrittenPath { get; private set; }

        public PrintFooterInfo? WrittenFooter { get; private set; }

        public int WriteCalls { get; private set; }

        /// Khác null thì WriteAsync trả một Task lỗi (như bộ ghi thật, vốn chạy trong Task.Run).
        public Exception? FailWith { get; init; }

        public void Write(IReadOnlyList<PrintSheet> sheets, Stream target, PrintFooterInfo footer) =>
            throw new NotSupportedException("Lệnh In chỉ được gọi WriteAsync.");

        public Task WriteAsync(
            IReadOnlyList<PrintSheet> sheets,
            string targetPath,
            PrintFooterInfo footer,
            CancellationToken ct = default)
        {
            WriteCalls++;
            if (FailWith is not null) return Task.FromException(FailWith);

            WrittenSheets.AddRange(sheets);
            WrittenPath = targetPath;
            WrittenFooter = footer;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLauncher : IFileLauncher
    {
        public string? Opened { get; private set; }

        public bool Result { get; init; } = true;

        public bool TryOpen(string path)
        {
            Opened = path;
            return Result;
        }
    }
}
