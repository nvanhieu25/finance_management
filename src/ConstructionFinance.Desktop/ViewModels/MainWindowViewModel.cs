using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.Printing;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.ViewModels.Decisions;
using ConstructionFinance.Desktop.ViewModels.Printing;
using ConstructionFinance.Desktop.ViewModels.ThongTri;
using ConstructionFinance.Desktop.ViewModels.TongHop;
using ConstructionFinance.Desktop.ViewModels.Vendors;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ConstructionFinance.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;
    private readonly INavigationService _nav;

    public MainWindowViewModel(IServiceProvider services, INavigationService nav)
    {
        _services = services;
        _nav = nav;
        _nav.CurrentContentChanged += (_, content) => CurrentContent = content;
    }

    // Landing navigation is driven by the View: MainWindow.OnNavLoaded sets the
    // NavigationView.SelectedItem, and real user clicks navigate via the ItemInvoked
    // event. This no-op (still called from App.axaml.cs) stays a no-op so there is no
    // second, racy navigation path competing with the View-driven one.
    public Task InitializeAsync() => Task.CompletedTask;

    [ObservableProperty] private string _title = "Quản lý Tài chính Công trình";
    [ObservableProperty] private string _statusText = $"DB: {AppPaths.DatabaseFile} | Sẵn sàng";
    [ObservableProperty] private object? _currentContent;

    public async Task NavigateToAsync(string tag)
    {
        switch (tag)
        {
            case "TongHop":
                var tvm = _services.GetRequiredService<TongHopViewModel>();
                _nav.Reset();
                _nav.NavigateTo(tvm);
                await tvm.LoadAsync();
                break;
            case "ThongTri":
                var ttvm = _services.GetRequiredService<ThongTriViewModel>();
                _nav.Reset();
                _nav.NavigateTo(ttvm);
                await ttvm.LoadAsync();
                break;
            case "Decisions":
                var dvm = _services.GetRequiredService<DecisionListViewModel>();
                _nav.Reset();
                _nav.NavigateTo(dvm);
                await dvm.LoadAsync();
                break;
            case "Vendors":
                var vvm = _services.GetRequiredService<VendorListViewModel>();
                _nav.Reset();
                _nav.NavigateTo(vvm);
                await vvm.LoadAsync();
                break;
            case "Projects":
                var pvm = _services.GetRequiredService<ViewModels.Projects.ProjectListViewModel>();
                pvm.SetContractId(0);
                _nav.Reset();
                _nav.NavigateTo(pvm);
                await pvm.LoadAsync();
                break;
            case "FinanceGrid":
                var fgvm = _services.GetRequiredService<ViewModels.Finance.FinanceProjectGridViewModel>();
                _nav.Reset();
                _nav.NavigateTo(fgvm);
                fgvm.InitializeNew();
                break;
            case "FinanceSummary":
                var fsvm = _services.GetRequiredService<ViewModels.Finance.FinanceSummaryViewModel>();
                _nav.Reset();
                _nav.NavigateTo(fsvm);
                await fsvm.LoadAsync();
                break;
            case "FinanceWorkbook":
                var fwvm = _services.GetRequiredService<ViewModels.Finance.FinanceWorkbookViewModel>();
                _nav.Reset();
                _nav.NavigateTo(fwvm);
                await fwvm.LoadAsync();
                break;
            case "WorkbookSummary":
                var wsvm = _services.GetRequiredService<ViewModels.Finance.WorkbookSummaryViewModel>();
                wsvm.ProjectActivationRequested += async (_, projectId) =>
                {
                    await NavigateToAsync("FinanceWorkbook");
                    if (_nav.CurrentContent is ViewModels.Finance.FinanceWorkbookViewModel fw)
                        fw.SelectProjectById(projectId);
                };
                _nav.Reset();
                _nav.NavigateTo(wsvm);
                await wsvm.LoadAsync();
                break;
            case "Settings":
                var svm = _services.GetRequiredService<ViewModels.Settings.SettingsViewModel>();
                _nav.Reset();
                _nav.NavigateTo(svm);
                await svm.InitializeAsync();
                break;
            default:
                _nav.Reset();
                _nav.NavigateTo(new PlaceholderViewModel { Tag = tag });
                break;
        }
    }

    [RelayCommand]
    private async Task OpenImportWizardAsync()
    {
        var wizard = _services.GetRequiredService<ImportWizardViewModel>();
        var dialog = _services.GetRequiredService<IDialogService>();
        var ok = await dialog.ShowDialogAsync(wizard);
        if (ok != true) return;

        var batchId = await wizard.SaveAndGetBatchIdAsync();
        if (batchId is null) return;

        await NavigateToAsync("TongHop");
        if (_nav.CurrentContent is TongHopViewModel tvm)
        {
            await tvm.LoadAsync();
            tvm.SelectedBatch = tvm.Batches.FirstOrDefault(b => b.Id == batchId.Value) ?? tvm.SelectedBatch;
        }
    }

    // ===== In =====

    private const string PrintTitle = "In";
    private const string PrintFileTitle = "In ra Excel";
    private const string PrintToolTipEnabled = "In màn hình đang mở hoặc toàn bộ sổ";
    private const string PrintToolTipDisabled = "Màn hình này không có nội dung để in";
    private const string PrintSaveLocationUnavailableMessage =
        "Không ghi được tệp vì nơi lưu không hợp lệ hoặc không truy cập được " +
        "(thư mục không còn tồn tại, ổ đĩa USB hoặc ổ mạng đã bị ngắt kết nối, hoặc đường dẫn quá dài). " +
        "Hãy kiểm tra kết nối ổ đĩa hoặc chọn nơi lưu khác, rồi thử lại.";

    /// Nút In ở khung chung sáng khi ÍT NHẤT MỘT trong bốn mục của nó dùng được; không thì mờ
    /// (vẫn nằm đúng chỗ). Chỉ ĐỌC trạng thái của màn đang mở và của danh sách máy in.
    public bool CanPrint =>
        CanExportCurrentSheet() || CanExportAll() || CanPrintCurrentSheet() || CanPrintAll();

    /// Nút In sáng thì tooltip nói nó làm gì; nút mờ thì tooltip nói vì sao.
    public string PrintToolTip => CanPrint ? PrintToolTipEnabled : PrintToolTipDisabled;

    [RelayCommand(CanExecute = nameof(CanExportCurrentSheet))]
    private Task ExportCurrentSheetAsync() => GuardPrintAsync(PrintFileTitle, async () =>
    {
        var page = (IPrintablePage)CurrentContent!;
        var sheet = page.BuildPrintSheet();
        // Hợp đồng IPrintablePage: PrintSheetName khác null thì BuildPrintSheet() phải trả một sheet, nên nhánh này
        // không nên xảy ra. Nhưng người dùng vừa bấm một nút: không được im lặng, cũng như ExportAllAsync.
        if (sheet is null)
        {
            await ShowNothingToPrintAsync();
            return;
        }
        await PrintAsync(new[] { sheet }, page.PrintSheetName!, footerViTriName: page.PrintSheetName);
    });

    private bool CanExportCurrentSheet() =>
        CurrentContent is IPrintablePage { PrintSheetName: { Length: > 0 } };

    [RelayCommand(CanExecute = nameof(CanExportAll))]
    private Task ExportAllAsync() => GuardPrintAsync(PrintFileTitle, async () =>
    {
        var workbook = (IPrintableWorkbook)CurrentContent!;
        var all = workbook.BuildAllSheets();
        // Nhánh rỗng gần như không xảy ra vì CanExportAll đã chặn nút, nhưng KHÔNG được
        // im lặng: user vừa bấm một nút, nên phải nói ra chuyện gì xảy ra.
        if (all.Sheets.Count == 0)
        {
            await ShowNothingToPrintAsync();
            return;
        }
        await PrintAsync(all.Sheets, "Toan bo", footerViTriName: null, warningAfterWrite: DescribeSkipped(all.Skipped));
    });

    private bool CanExportAll() =>
        CurrentContent is IPrintableWorkbook wb && wb.CanPrintAll;
        // KHÔNG gọi BuildAllSheets() ở đây: nó làm mới tab tổng hợp, nên đặt trong
        // CanExecute là nhét tác dụng phụ vào một phép hỏi bị gọi rất thường xuyên.

    private const string PrintPaperTitle = "In ra máy in";
    private const string NoPrinterMessage =
        "Máy này chưa cài máy in nào. Bạn có thể dùng \"Xuất Excel\" để lưu tệp rồi in từ máy khác.";

    [RelayCommand(CanExecute = nameof(CanPrintCurrentSheet))]
    private Task PrintCurrentSheetAsync() => GuardPrintAsync(PrintPaperTitle, async () =>
    {
        var page = (IPrintablePage)CurrentContent!;
        var sheet = page.BuildPrintSheet();
        if (sheet is null)
        {
            await ShowNothingToPrintAsync();
            return;
        }
        await PrintToPrinterAsync(new[] { sheet }, footerViTriName: page.PrintSheetName);
    });

    /// Đích net8.0 (bộ test) không đăng ký IPrinterPort nào, nên hai lệnh này mờ ở đó thay vì
    /// ném NullReference giữa một lần chạy test. Bản phát hành luôn là net8.0-windows.
    private bool CanPrintCurrentSheet() =>
        HasPrinterSupport && CurrentContent is IPrintablePage { PrintSheetName: { Length: > 0 } };

    [RelayCommand(CanExecute = nameof(CanPrintAll))]
    private Task PrintAllAsync() => GuardPrintAsync(PrintPaperTitle, async () =>
    {
        var workbook = (IPrintableWorkbook)CurrentContent!;
        var all = workbook.BuildAllSheets();
        if (all.Sheets.Count == 0)
        {
            await ShowNothingToPrintAsync();
            return;
        }
        await PrintToPrinterAsync(all.Sheets, footerViTriName: null, warningBeforePrint: DescribeSkipped(all.Skipped));
    });

    private bool CanPrintAll() =>
        HasPrinterSupport && CurrentContent is IPrintableWorkbook { CanPrintAll: true };

    private bool HasPrinterSupport =>
        _services.GetService<IPrinterPort>() is not null
        && _services.GetService<IPrinterCatalog>() is not null;

    /// Đường chung của cả hai lệnh in giấy: cảnh báo (nếu có) → chọn máy in → gửi lệnh in.
    ///
    /// `warningBeforePrint` hiện TRƯỚC hộp thoại, khác đường ghi tệp (ở đó cảnh báo hiện sau
    /// khi ghi). Cố ý: tệp đã nằm trên đĩa thì đọc cảnh báo rồi vẫn xử lý được, còn giấy đã
    /// chạy ra thì không rút lại được — nên người dùng phải đọc trước khi quyết.
    private async Task PrintToPrinterAsync(
        IReadOnlyList<PrintSheet> sheets, string? footerViTriName, string? warningBeforePrint = null)
    {
        var dialog = _services.GetRequiredService<IDialogService>();
        var catalog = _services.GetRequiredService<IPrinterCatalog>();

        var chooser = new ChonMayInViewModel(catalog);
        if (!chooser.CoMayIn)
        {
            await dialog.ShowMessageAsync(PrintPaperTitle, NoPrinterMessage);
            return;
        }

        if (warningBeforePrint is not null)
            await dialog.ShowMessageAsync(PrintPaperTitle, warningBeforePrint);

        if (await dialog.ShowDialogAsync(chooser) != true) return;   // Hủy: im lặng, không in

        var printerName = chooser.SelectedPrinter;
        if (string.IsNullOrWhiteSpace(printerName))
        {
            await dialog.ShowMessageAsync(PrintPaperTitle, NoPrinterMessage);
            return;
        }

        var workbook = CurrentContent as IPrintableWorkbook;
        var footer = new PrintFooterInfo(workbook?.PrintFooterProjectName ?? "", footerViTriName);
        var document = _services.GetRequiredService<IMigraDocBuilder>().Build(sheets, footer);

        try
        {
            await _services.GetRequiredService<IPrinterPort>()
                .PrintAsync(new PrinterJob(document, printerName, chooser.Copies));
        }
        catch (PrintJobFailedException ex)
        {
            Log.Error(ex, "Máy in {Printer} báo lỗi", ex.PrinterName);
            await dialog.ShowMessageAsync(PrintPaperTitle,
                $"Máy in \"{ex.PrinterName}\" không in được. {ex.InnerException?.Message} " +
                "Hãy kiểm tra máy in đã bật, có giấy và còn kết nối, rồi thử lại.");
        }
    }

    private Task ShowNothingToPrintAsync() =>
        _services.GetRequiredService<IDialogService>().ShowMessageAsync(PrintTitle, "Chưa có nội dung nào để in.");

    /// Vỏ của cả hai lệnh In. Lỗi nào lọt ra khỏi một AsyncRelayCommand đều bị CommunityToolkit ném lại lên luồng giao
    /// diện (mặc định của nó) và làm sập cả app, trong khi người dùng có thể đang có sổ chưa lưu. Nên lỗi ở BẤT KỲ
    /// bước nào — dựng sheet của màn, lấy dịch vụ, hộp thoại chọn nơi lưu... — dừng ở đây: ghi nhật ký rồi báo bằng
    /// một câu chung. Lỗi ở bước GHI TỆP thì PrintAsync đã xử lý riêng từ trước (câu riêng, nhật ký có đường dẫn).
    private async Task GuardPrintAsync(string title, Func<Task> print)
    {
        try
        {
            await print();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Lệnh In gặp lỗi bất ngờ");
            await _services.GetRequiredService<IDialogService>().ShowMessageAsync(title,
                "Không in được do lỗi bất ngờ. Chi tiết lỗi đã được ghi vào nhật ký ứng dụng.");
        }
    }

    /// Đường chung của cả hai lệnh: chọn nơi lưu → ghi → mở bằng ứng dụng mặc định.
    /// `fileLabel` chỉ để đặt tên tệp gợi ý. `footerViTriName` là tên Vị trí in ở chân trang: chỉ có khi in riêng một
    /// sheet, còn In toàn bộ thì null (nhãn "Toan bo" của tên tệp không phải tên một Vị trí). `warningAfterWrite` (chỉ In toàn bộ
    /// có) là lời cảnh báo hiện sau khi ghi xong, nói bộ tệp thiếu gì.
    private async Task PrintAsync(
        IReadOnlyList<PrintSheet> sheets, string fileLabel, string? footerViTriName, string? warningAfterWrite = null)
    {
        var dialog = _services.GetRequiredService<IDialogService>();
        var writer = _services.GetRequiredService<IPrintWorkbookWriter>();
        var launcher = _services.GetRequiredService<IFileLauncher>();

        // Hai tên khác nhau cho hai người đọc khác nhau: tên TỆP lấy mã dự án (PrintWorkbookName), còn chân trang trên giấy lấy
        // TÊN dự án (PrintFooterProjectName), vì người nhận bản in không biết mã nội bộ.
        var workbook = CurrentContent as IPrintableWorkbook;
        var prefix = workbook?.PrintWorkbookName ?? "";
        var footerProjectName = workbook?.PrintFooterProjectName ?? "";
        var suggested = PrintFileName.Suggest(prefix, fileLabel, DateTime.Now);

        var path = await dialog.PickSaveFileAsync(PrintFileTitle, suggested, new[] { ".xlsx" });
        if (path is null) return;          // người dùng bấm Hủy — im lặng, không báo lỗi

        var footer = new PrintFooterInfo(footerProjectName, footerViTriName);
        try
        {
            await writer.WriteAsync(sheets, path, footer);
        }
        // Thứ tự các nhánh catch quyết định câu người dùng đọc, và câu thông báo không được nói sai: DirectoryNotFoundException,
        // PathTooLongException và DriveNotFoundException đều là CON của IOException, nên phải đứng TRƯỚC nhánh IOException
        // (trình biên dịch bắt buộc, CS0160, nên không thể đảo nhầm mà vẫn build). Nếu không, một thư mục không còn tồn tại, hay
        // một ổ USB vừa bị rút, sẽ nghe "đóng tệp rồi thử lại" hoặc "ổ đã đầy" về một chuyện chẳng liên quan. Không phân biệt
        // xung đột chia sẻ theo HResult: mã đó khác nhau giữa các hệ điều hành (đo trên Linux: 0x0B; Windows dùng 0x20/0x21 theo
        // tài liệu Win32, chưa chạy thử ở đây), và đoán sai thì ca hay gặp nhất, tệp đang mở trong Excel, sẽ rơi xuống câu mơ hồ.
        catch (DirectoryNotFoundException ex)
        {
            LogPrintIoFailure(ex, path, "thư mục không tồn tại");
            await dialog.ShowMessageAsync(PrintFileTitle, PrintSaveLocationUnavailableMessage);
            return;
        }
        catch (PathTooLongException ex)
        {
            LogPrintIoFailure(ex, path, "đường dẫn quá dài");
            await dialog.ShowMessageAsync(PrintFileTitle, PrintSaveLocationUnavailableMessage);
            return;
        }
        catch (DriveNotFoundException ex)
        {
            LogPrintIoFailure(ex, path, "ổ đĩa không tồn tại hoặc đã ngắt kết nối");
            await dialog.ShowMessageAsync(PrintFileTitle, PrintSaveLocationUnavailableMessage);
            return;
        }
        catch (IOException ex)
        {
            // Còn lại của IOException: ba nguyên nhân hay gặp mà không thể phân biệt nếu không dựa vào mã riêng từng hệ điều
            // hành (xung đột chia sẻ, ổ đầy, ổ mạng ngắt; ổ mạng offline trên Windows rất có thể ra IOException trơn, chưa
            // kiểm). Câu thông báo nói thật cả ba, mỗi nguyên nhân kèm việc nên làm; chỗ phân biệt chính xác nằm ở nhật ký.
            LogPrintIoFailure(ex, path, "lỗi vào/ra: tệp đang mở, ổ đầy hoặc ổ mạng ngắt kết nối");
            await dialog.ShowMessageAsync(PrintFileTitle,
                "Không ghi được tệp. Có thể tệp đang được mở trong chương trình khác, ổ đĩa đã đầy, hoặc ổ mạng bị ngắt kết nối. " +
                "Hãy đóng tệp nếu đang mở, giải phóng dung lượng ổ đĩa, hoặc kiểm tra kết nối ổ mạng, rồi thử lại.");
            return;
        }
        catch (Exception ex)
        {
            // Mọi lỗi còn lại của bước ghi (không có quyền ghi, ổ đầy, tên sheet/chân trang mang surrogate đứt đôi làm bộ
            // ghi .xlsx ném ArgumentException...): nút In không bao giờ được làm app sập hay im lặng. Người dùng nhận
            // một câu chung, chi tiết nằm trong nhật ký.
            Log.Error(ex, "Không ghi được tệp in {Path}", path);
            await dialog.ShowMessageAsync(PrintFileTitle,
                "Không ghi được tệp in. Thử lại hoặc chọn nơi lưu khác. Chi tiết lỗi đã được ghi vào nhật ký ứng dụng.");
            return;
        }

        // Cảnh báo "đã bỏ qua sheet nào": hiện NGAY sau khi ghi xong và TRƯỚC khi mở tệp. Mở tệp đưa Excel lên trên cùng và che mất
        // hộp thoại của app, mà một cảnh báo không ai nhìn thấy cũng như không có cảnh báo (người dùng gom bộ hồ sơ rồi gửi đi,
        // và người nhận mới là người phát hiện thiếu sheet). Ghi không thành thì đã return ở trên: chỉ báo lỗi ghi.
        if (warningAfterWrite is not null)
            await dialog.ShowMessageAsync(PrintFileTitle, warningAfterWrite);

        if (!launcher.TryOpen(path))
            await dialog.ShowMessageAsync(PrintFileTitle, $"Đã lưu tệp tại:\n{path}");
    }

    /// Lời cảnh báo cho các trang "In toàn bộ" đã bỏ qua; null khi không bỏ qua trang nào. Một trang: đúng chữ của spec §8
    /// ("Đã bỏ qua GĐNTT: chưa chọn gói thầu."). Nhiều trang: liệt kê từng trang, để người dùng thấy bộ tệp thiếu gì mà không
    /// phải đối chiếu từng tab.
    private static string? DescribeSkipped(IReadOnlyList<SkippedPrintPage> skipped) => skipped.Count switch
    {
        0 => null,
        1 => $"Đã bỏ qua {DescribeSkippedPage(skipped[0])}.",
        _ => $"Đã bỏ qua {skipped.Count} sheet:\n" + string.Join("\n", skipped.Select(page => "- " + DescribeSkippedPage(page))),
    };

    private static string DescribeSkippedPage(SkippedPrintPage page) =>
        string.IsNullOrWhiteSpace(page.Reason) ? page.Title : $"{page.Title}: {page.Reason}";

    /// Nhật ký cho các lỗi ghi họ IOException. Người dùng chỉ đọc một câu (câu của nhánh IOException che cả ba nguyên nhân);
    /// nhật ký là thứ duy nhất cho người hỗ trợ biết đó là xung đột chia sẻ, ổ đầy, ổ bị rút hay ổ mạng ngắt. HResult nằm ngay trong dòng
    /// vì cùng một lỗi mang mã khác nhau theo hệ điều hành (xung đột chia sẻ: đo trên Linux là 0x0B, Windows là 0x20/0x21 theo
    /// tài liệu Win32). Warning chứ không phải Error: đây là tình huống của môi trường người dùng, không phải lỗi của ứng dụng.
    private static void LogPrintIoFailure(Exception ex, string path, string cause) =>
        Log.Warning(ex, "Không ghi được tệp in ({Cause}): {Path}, HResult 0x{HResult:X8}", cause, path, ex.HResult);

    // Một handler duy nhất, thứ tự viết rõ ra: không dựa vào việc CommunityToolkit gọi overload nào trước (xem cùng
    // quy ước ở FinanceWorkbookViewModel.OnSelectedViTriPageChanged).
    partial void OnCurrentContentChanged(object? oldValue, object? newValue)
    {
        // 1. Gỡ handler khỏi màn vừa rời. Không gỡ thì màn cũ vẫn báo vào đây và bị giữ sống mãi — đúng loại lỗi
        //    review 7B đã bắt được với KeyDown.
        if (oldValue is INotifyPropertyChanged oldNotifier)
            oldNotifier.PropertyChanged -= OnContentPropertyChanged;

        // 2. Nghe màn mới (nếu nó có báo thay đổi được). Màn mới là null thì chỉ có bước 1.
        if (newValue is INotifyPropertyChanged newNotifier)
            newNotifier.PropertyChanged += OnContentPropertyChanged;

        // 3. Báo CUỐI CÙNG: đổi màn là đổi khả năng in, nên người nghe (nút In) đọc giá trị của đúng màn mới.
        RaisePrintStateChanged();
    }

    // Màn đang mở tự đổi trạng thái in mà không đổi màn (đổi tên Vị trí, tích gói thầu cho GĐNTT, workbook có thêm/bớt
    // tab in được): chuyển tiếp, nếu không nút In sáng/mờ lệch cho tới lần đổi màn sau. Chỉ hai cái tên mà nút In đọc;
    // màn thật bắn PropertyChanged liên tục cho các thuộc tính khác (IsDirty mỗi lần gõ phím...) và nút không cần biết.
    // Không so giá trị: báo thừa thì được, bỏ sót mới là lỗi.
    private void OnContentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IPrintablePage.PrintSheetName) or nameof(IPrintableWorkbook.CanPrintAll))
            RaisePrintStateChanged();
    }

    /// CanExecuteChanged là thứ Avalonia nghe để bật/mờ lại từng mục menu; CanPrint/PrintToolTip là thứ nút In nghe.
    /// Bốn lệnh, bốn lần báo: thiếu một là một mục menu đứng im với trạng thái của màn trước.
    private void RaisePrintStateChanged()
    {
        ExportCurrentSheetCommand.NotifyCanExecuteChanged();
        ExportAllCommand.NotifyCanExecuteChanged();
        PrintCurrentSheetCommand.NotifyCanExecuteChanged();
        PrintAllCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanPrint));
        OnPropertyChanged(nameof(PrintToolTip));
    }
}

public class PlaceholderViewModel : ViewModelBase
{
    public string Tag { get; set; } = "";
}
