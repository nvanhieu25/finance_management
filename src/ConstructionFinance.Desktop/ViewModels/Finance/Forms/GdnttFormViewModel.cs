using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Domain.Finance;
using Serilog;

namespace ConstructionFinance.Desktop.ViewModels.Finance.Forms;

/// Tab xem trước Giấy đề nghị thanh toán vốn đầu tư. Một giấy = một gói thầu, nên
/// khi lựa chọn trải nhiều gói (hoặc chưa chọn gì) thì hiện dải cảnh báo và chặn xuất.
public partial class GdnttFormViewModel : ViewModelBase
{
    private readonly IFormExcelExporter _exporter;
    private readonly IDialogService _dialog;

    private IReadOnlyList<FormDotRow> _rows = Array.Empty<FormDotRow>();
    private GdnttDeclaration _declaration = new();
    private FormOrgInfo _org = new(null, null);
    private string _projectName = "";
    private GdnttForm? _form;

    public GdnttFormViewModel(IFormExcelExporter exporter, IDialogService dialog)
    {
        _exporter = exporter;
        _dialog = dialog;
        Picker.SelectionChanged += (_, _) => Rebuild();
    }

    public FormDotPickerViewModel Picker { get; } = new(Fund.NgoaiHoi);

    public event EventHandler? DeclarationChanged;

    public string? ErrorMessage { get; private set; }
    public bool CanExport => _form is not null;

    public string PackageName => _form?.PackageName ?? "";
    public string ContractorName => _form?.ContractorName ?? "";
    public string BankAccountText => _form?.BankAccount ?? "";
    public string ContractTotalText => Money(_form?.ContractTotal);
    public string CumulativeBeforeText => Money(_form?.CumulativeBefore);
    public string UnrecoveredBeforeText => Money(_form?.UnrecoveredAdvanceBefore);
    public string RequestedText => Money(_form?.RequestedThisPeriod);
    public string RequestedInWords => _form?.RequestedInWords ?? "";
    public string RecoveredText => Money(_form?.RecoveredThisPeriod);
    public string PaidToContractorText => Money(_form?.PaidToContractor);
    public string NguonVonText => "Quỹ dự trữ ngoại hối";

    /// Tên hai đơn vị in ở đầu GĐNTT (Cài đặt → dùng chung mọi dự án).
    public string DonViCapTrenText => _org.DonViCapTren ?? "";
    public string DonViCoSoText => _org.DonViCoSo ?? "";

    public string? So { get => _declaration.So; set => SetText(_declaration.So, value, v => _declaration.So = v); }
    public string? NgayThangNam { get => _declaration.NgayThangNam; set => SetText(_declaration.NgayThangNam, value, v => _declaration.NgayThangNam = v); }
    public string? KinhGui { get => _declaration.KinhGui; set => SetText(_declaration.KinhGui, value, v => _declaration.KinhGui = v); }
    public string? TenDuAnCongTrinh { get => _declaration.TenDuAnCongTrinh; set => SetText(_declaration.TenDuAnCongTrinh, value, v => _declaration.TenDuAnCongTrinh = v); }
    public string? ChuDauTu { get => _declaration.ChuDauTu; set => SetText(_declaration.ChuDauTu, value, v => _declaration.ChuDauTu = v); }
    public string? CanCuQuyetDinh { get => _declaration.CanCuQuyetDinh; set => SetText(_declaration.CanCuQuyetDinh, value, v => _declaration.CanCuQuyetDinh = v); }
    public string? CanCuHopDong { get => _declaration.CanCuHopDong; set => SetText(_declaration.CanCuHopDong, value, v => _declaration.CanCuHopDong = v); }
    public string? CanCuGiayDeNghi { get => _declaration.CanCuGiayDeNghi; set => SetText(_declaration.CanCuGiayDeNghi, value, v => _declaration.CanCuGiayDeNghi = v); }
    public string? ThuocKeHoach { get => _declaration.ThuocKeHoach; set => SetText(_declaration.ThuocKeHoach, value, v => _declaration.ThuocKeHoach = v); }
    public string? TaiNganHang { get => _declaration.TaiNganHang; set => SetText(_declaration.TaiNganHang, value, v => _declaration.TaiNganHang = v); }

    public string ChuyenTienBaoHanhText { get => MoneyText.Format(_declaration.ChuyenTienBaoHanh); set => SetMoney(_declaration.ChuyenTienBaoHanh, value, v => _declaration.ChuyenTienBaoHanh = v); }
    public string ChuyenTienTamGiuText { get => MoneyText.Format(_declaration.ChuyenTienTamGiu); set => SetMoney(_declaration.ChuyenTienTamGiu, value, v => _declaration.ChuyenTienTamGiu = v); }

    public void Refresh(
        IReadOnlyList<FormDotRow> rows,
        IReadOnlyList<LedgerRowViewModel> sources,
        GdnttDeclaration declaration,
        FormOrgInfo org,
        string projectName)
    {
        _rows = rows;
        _declaration = declaration;
        _org = org;
        _projectName = projectName;

        Picker.Refresh(rows, sources);
        RaiseDeclarationProperties();
        OnPropertyChanged(nameof(DonViCapTrenText));
        OnPropertyChanged(nameof(DonViCoSoText));
        Rebuild();
    }

    public GdnttDeclaration ToDeclaration() => _declaration;

    private static string Money(decimal? value) => value is null ? "" : MoneyText.Format(value.Value);

    /// Model biểu mẫu theo trạng thái hiện tại, hoặc null khi lựa chọn đợt chưa hợp lệ (chưa chọn đợt nào,
    /// hoặc trải nhiều gói thầu) — đúng lúc CanExport == false.
    public GdnttForm? BuildForm() => BuildResult().Form;

    /// Chỗ dựng duy nhất, dùng chung cho bản xem trước, xuất Excel và in. Trả cả kết quả chứ không chỉ model
    /// vì bản xem trước và nút xuất còn cần thông báo lỗi để nói vì sao chưa dựng được.
    private GdnttBuildResult BuildResult() =>
        GdnttBuilder.Build(_rows, Picker.SelectedKeys, _declaration, _org);

    private void Rebuild()
    {
        var result = BuildResult();
        _form = result.Form;
        ErrorMessage = result.Error;

        foreach (var name in new[]
                 {
                     nameof(ErrorMessage), nameof(CanExport), nameof(PackageName), nameof(ContractorName),
                     nameof(BankAccountText), nameof(ContractTotalText), nameof(CumulativeBeforeText),
                     nameof(UnrecoveredBeforeText), nameof(RequestedText), nameof(RequestedInWords),
                     nameof(RecoveredText), nameof(PaidToContractorText)
                 })
            OnPropertyChanged(name);
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var result = BuildResult();
        if (result.Form is null)
        {
            await _dialog.ShowMessageAsync("Chưa xuất được", result.Error ?? "Lựa chọn đợt chưa hợp lệ.");
            return;
        }

        var suggested = $"GDNTT_{FileNames.Sanitize(_projectName)}_{DateTime.Now:yyyyMMdd}.xlsx";
        var path = await _dialog.PickSaveFileAsync("Xuất Giấy đề nghị thanh toán", suggested, new[] { "xlsx" });
        if (path is null) return;

        try
        {
            await _exporter.ExportGdnttAsync(result.Form, path);
            await _dialog.ShowMessageAsync("Xuất Excel", $"Đã xuất Giấy đề nghị thanh toán ra:\n{path}");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Xuất GĐNTT thất bại");
            await _dialog.ShowMessageAsync("Lỗi", $"Không xuất được Giấy đề nghị thanh toán: {ex.Message}");
        }
    }

    private void SetText(string? current, string? value, Action<string?> assign, [CallerMemberName] string? name = null)
    {
        if (current == value) return;
        assign(value);
        OnPropertyChanged(name);
        Rebuild();
        DeclarationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetMoney(decimal current, string? raw, Action<decimal> assign, [CallerMemberName] string? name = null)
    {
        if (!MoneyText.TryParse(raw, out var parsed)) return;
        if (current == parsed) return;
        assign(parsed);
        OnPropertyChanged(name);
        Rebuild();
        DeclarationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseDeclarationProperties()
    {
        foreach (var name in new[]
                 {
                     nameof(So), nameof(NgayThangNam), nameof(KinhGui), nameof(TenDuAnCongTrinh),
                     nameof(ChuDauTu), nameof(CanCuQuyetDinh), nameof(CanCuHopDong), nameof(CanCuGiayDeNghi),
                     nameof(ThuocKeHoach), nameof(TaiNganHang), nameof(ChuyenTienBaoHanhText),
                     nameof(ChuyenTienTamGiuText)
                 })
            OnPropertyChanged(name);
    }
}
