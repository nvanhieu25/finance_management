using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.Printing;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.ViewModels.Decisions;
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

    /// Cả nút In ở khung chung sáng khi ÍT NHẤT MỘT trong hai mục của nó dùng được; không thì mờ (vẫn nằm đúng chỗ).
    /// Chỉ ĐỌC trạng thái của màn đang mở, như hai CanExecute bên dưới.
    public bool CanPrint => CanPrintCurrentSheet() || CanPrintAll();

    /// Nút In sáng thì tooltip nói nó làm gì; nút mờ thì tooltip nói vì sao.
    public string PrintToolTip => CanPrint ? PrintToolTipEnabled : PrintToolTipDisabled;

    [RelayCommand(CanExecute = nameof(CanPrintCurrentSheet))]
    private Task PrintCurrentSheetAsync() => GuardPrintAsync(async () =>
    {
        var page = (IPrintablePage)CurrentContent!;
        var sheet = page.BuildPrintSheet();
        // Hợp đồng IPrintablePage: PrintSheetName khác null thì BuildPrintSheet() phải trả một sheet, nên nhánh này
        // không nên xảy ra. Nhưng người dùng vừa bấm một nút: không được im lặng, cũng như PrintAllAsync.
        if (sheet is null)
        {
            await ShowNothingToPrintAsync();
            return;
        }
        await PrintAsync(new[] { sheet }, page.PrintSheetName!, footerViTriName: page.PrintSheetName);
    });

    private bool CanPrintCurrentSheet() =>
        CurrentContent is IPrintablePage { PrintSheetName: { Length: > 0 } };

    [RelayCommand(CanExecute = nameof(CanPrintAll))]
    private Task PrintAllAsync() => GuardPrintAsync(async () =>
    {
        var workbook = (IPrintableWorkbook)CurrentContent!;
        var sheets = workbook.BuildAllSheets();
        // Nhánh rỗng gần như không xảy ra vì CanPrintAll đã chặn nút, nhưng KHÔNG được
        // im lặng: user vừa bấm một nút, nên phải nói ra chuyện gì xảy ra.
        if (sheets.Count == 0)
        {
            await ShowNothingToPrintAsync();
            return;
        }
        await PrintAsync(sheets, "Toan bo", footerViTriName: null);
    });

    private bool CanPrintAll() =>
        CurrentContent is IPrintableWorkbook wb && wb.CanPrintAll;
        // KHÔNG gọi BuildAllSheets() ở đây: nó làm mới tab tổng hợp, nên đặt trong
        // CanExecute là nhét tác dụng phụ vào một phép hỏi bị gọi rất thường xuyên.

    private Task ShowNothingToPrintAsync() =>
        _services.GetRequiredService<IDialogService>().ShowMessageAsync(PrintTitle, "Chưa có nội dung nào để in.");

    /// Vỏ của cả hai lệnh In. Lỗi nào lọt ra khỏi một AsyncRelayCommand đều bị CommunityToolkit ném lại lên luồng giao
    /// diện (mặc định của nó) và làm sập cả app, trong khi người dùng có thể đang có sổ chưa lưu. Nên lỗi ở BẤT KỲ
    /// bước nào — dựng sheet của màn, lấy dịch vụ, hộp thoại chọn nơi lưu... — dừng ở đây: ghi nhật ký rồi báo bằng
    /// một câu chung. Lỗi ở bước GHI TỆP thì PrintAsync đã xử lý riêng từ trước (câu riêng, nhật ký có đường dẫn).
    private async Task GuardPrintAsync(Func<Task> print)
    {
        try
        {
            await print();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Lệnh In gặp lỗi bất ngờ");
            await _services.GetRequiredService<IDialogService>().ShowMessageAsync(PrintFileTitle,
                "Không in được do lỗi bất ngờ. Chi tiết lỗi đã được ghi vào nhật ký ứng dụng.");
        }
    }

    /// Đường chung của cả hai lệnh: chọn nơi lưu → ghi → mở bằng ứng dụng mặc định.
    /// `fileLabel` chỉ để đặt tên tệp gợi ý. `footerViTriName` là tên Vị trí in ở chân trang: chỉ có khi in riêng một
    /// sheet, còn In toàn bộ thì null (nhãn "Toan bo" của tên tệp không phải tên một Vị trí).
    private async Task PrintAsync(IReadOnlyList<PrintSheet> sheets, string fileLabel, string? footerViTriName)
    {
        var dialog = _services.GetRequiredService<IDialogService>();
        var writer = _services.GetRequiredService<IPrintWorkbookWriter>();
        var launcher = _services.GetRequiredService<IFileLauncher>();

        var prefix = (CurrentContent as IPrintableWorkbook)?.PrintWorkbookName ?? "";
        var suggested = PrintFileName.Suggest(prefix, fileLabel, DateTime.Now);

        var path = await dialog.PickSaveFileAsync(PrintFileTitle, suggested, new[] { ".xlsx" });
        if (path is null) return;          // người dùng bấm Hủy — im lặng, không báo lỗi

        var footer = new PrintFooterInfo(prefix, footerViTriName);
        try
        {
            await writer.WriteAsync(sheets, path, footer);
        }
        catch (IOException)
        {
            await dialog.ShowMessageAsync(PrintFileTitle,
                "Tệp đang được mở trong chương trình khác. Đóng tệp rồi thử lại.");
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

        if (!launcher.TryOpen(path))
            await dialog.ShowMessageAsync(PrintFileTitle, $"Đã lưu tệp tại:\n{path}");
    }

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
    private void RaisePrintStateChanged()
    {
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
