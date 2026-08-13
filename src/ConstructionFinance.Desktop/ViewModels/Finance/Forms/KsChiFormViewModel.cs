using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Domain.Finance;
using Serilog;

namespace ConstructionFinance.Desktop.ViewModels.Finance.Forms;

/// Tab xem trước phần ghi của cơ quan kiểm soát, thanh toán. Lấy cả hai nguồn nên
/// picker không lọc Fund.
public partial class KsChiFormViewModel : ViewModelBase
{
    private readonly IFormExcelExporter _exporter;
    private readonly IDialogService _dialog;

    private IReadOnlyList<FormDotRow> _rows = Array.Empty<FormDotRow>();
    private KsChiDeclaration _declaration = new();
    private string _projectName = "";
    private KsChiForm _form = Empty();

    public KsChiFormViewModel(IFormExcelExporter exporter, IDialogService dialog)
    {
        _exporter = exporter;
        _dialog = dialog;
        Picker.SelectionChanged += (_, _) => Rebuild();
    }

    public FormDotPickerViewModel Picker { get; } = new(null);

    public event EventHandler? DeclarationChanged;

    public string AcceptedTotalText => MoneyText.Format(_form.Accepted.Total);
    public string AcceptedNsnnText => MoneyText.Format(_form.Accepted.NSNN);
    public string AcceptedNgoaiHoiText => MoneyText.Format(_form.Accepted.NgoaiHoi);
    public string RecoveredTotalText => MoneyText.Format(_form.Recovered.Total);
    public string RecoveredNsnnText => MoneyText.Format(_form.Recovered.NSNN);
    public string RecoveredNgoaiHoiText => MoneyText.Format(_form.Recovered.NgoaiHoi);
    public string PaidTotalText => MoneyText.Format(_form.PaidToBeneficiary.Total);
    public string PaidNsnnText => MoneyText.Format(_form.PaidToBeneficiary.NSNN);
    public string PaidNgoaiHoiText => MoneyText.Format(_form.PaidToBeneficiary.NgoaiHoi);
    public string PaidInWords => _form.PaidInWords;
    public string GhiChuText => _form.GhiChu;

    public string? NgayNhanGiayDeNghi { get => _declaration.NgayNhanGiayDeNghi; set => SetText(_declaration.NgayNhanGiayDeNghi, value, v => _declaration.NgayNhanGiayDeNghi = v); }
    public string? MucTieuMuc1 { get => _declaration.MucTieuMuc1; set => SetText(_declaration.MucTieuMuc1, value, v => _declaration.MucTieuMuc1 = v); }
    public string? MucTieuMuc2 { get => _declaration.MucTieuMuc2; set => SetText(_declaration.MucTieuMuc2, value, v => _declaration.MucTieuMuc2 = v); }
    public string? MucTieuMuc3 { get => _declaration.MucTieuMuc3; set => SetText(_declaration.MucTieuMuc3, value, v => _declaration.MucTieuMuc3 = v); }
    public string? MucTieuMuc4 { get => _declaration.MucTieuMuc4; set => SetText(_declaration.MucTieuMuc4, value, v => _declaration.MucTieuMuc4 = v); }
    public string? LyDoTuChoi { get => _declaration.LyDoTuChoi; set => SetText(_declaration.LyDoTuChoi, value, v => _declaration.LyDoTuChoi = v); }
    public string? GhiChu { get => _declaration.GhiChu; set => SetText(_declaration.GhiChu, value, v => _declaration.GhiChu = v); }

    // Ô tiền: facade chuỗi, không bind thẳng decimal (bài học 6A).
    public string ThuHoiCacNamTruocText { get => MoneyText.Format(_declaration.ThuHoiCacNamTruoc); set => SetMoney(_declaration.ThuHoiCacNamTruoc, value, v => _declaration.ThuHoiCacNamTruoc = v); }
    public string ThuHoiNamNayText { get => MoneyText.Format(_declaration.ThuHoiNamNay); set => SetMoney(_declaration.ThuHoiNamNay, value, v => _declaration.ThuHoiNamNay = v); }
    public string ThueGTGTText { get => MoneyText.Format(_declaration.ThueGTGT); set => SetMoney(_declaration.ThueGTGT, value, v => _declaration.ThueGTGT = v); }
    public string SoTuChoiText { get => MoneyText.Format(_declaration.SoTuChoi); set => SetMoney(_declaration.SoTuChoi, value, v => _declaration.SoTuChoi = v); }

    public void Refresh(
        IReadOnlyList<FormDotRow> rows,
        IReadOnlyList<LedgerRowViewModel> sources,
        KsChiDeclaration declaration,
        string projectName)
    {
        _rows = rows;
        _declaration = declaration;
        _projectName = projectName;

        Picker.Refresh(rows, sources);
        RaiseDeclarationProperties();
        Rebuild();
    }

    public KsChiDeclaration ToDeclaration() => _declaration;

    private static KsChiForm Empty() => KsChiBuilder.Build(
        Array.Empty<FormDotRow>(), new HashSet<FormDotKey>(), null);

    private void Rebuild()
    {
        _form = KsChiBuilder.Build(_rows, Picker.SelectedKeys, _declaration);
        foreach (var name in new[]
                 {
                     nameof(AcceptedTotalText), nameof(AcceptedNsnnText), nameof(AcceptedNgoaiHoiText),
                     nameof(RecoveredTotalText), nameof(RecoveredNsnnText), nameof(RecoveredNgoaiHoiText),
                     nameof(PaidTotalText), nameof(PaidNsnnText), nameof(PaidNgoaiHoiText),
                     nameof(PaidInWords), nameof(GhiChuText)
                 })
            OnPropertyChanged(name);
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var suggested = $"KSChi_{FileNames.Sanitize(_projectName)}_{DateTime.Now:yyyyMMdd}.xlsx";
        var path = await _dialog.PickSaveFileAsync("Xuất Kiểm soát chi", suggested, new[] { "xlsx" });
        if (path is null) return;

        try
        {
            await _exporter.ExportKsChiAsync(KsChiBuilder.Build(_rows, Picker.SelectedKeys, _declaration), path);
            await _dialog.ShowMessageAsync("Xuất Excel", $"Đã xuất Kiểm soát chi ra:\n{path}");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Xuất KS chi thất bại");
            await _dialog.ShowMessageAsync("Lỗi", $"Không xuất được Kiểm soát chi: {ex.Message}");
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
                     nameof(NgayNhanGiayDeNghi), nameof(MucTieuMuc1), nameof(MucTieuMuc2), nameof(MucTieuMuc3),
                     nameof(MucTieuMuc4), nameof(LyDoTuChoi), nameof(GhiChu), nameof(ThuHoiCacNamTruocText),
                     nameof(ThuHoiNamNayText), nameof(ThueGTGTText), nameof(SoTuChoiText)
                 })
            OnPropertyChanged(name);
    }
}
