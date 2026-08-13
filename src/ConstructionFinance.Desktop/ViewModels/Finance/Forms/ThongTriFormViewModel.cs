using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Domain.Finance;
using Serilog;

namespace ConstructionFinance.Desktop.ViewModels.Finance.Forms;

/// Tab xem trước Thông tri: panel chọn đợt NSNN + các ô nhập tay + bảng in tính sống.
/// Không tự tính con số nào — mọi thứ đi qua ThongTriBuilder.
public partial class ThongTriFormViewModel : ViewModelBase
{
    private readonly IFormExcelExporter _exporter;
    private readonly IDialogService _dialog;

    private IReadOnlyList<FormDotRow> _rows = Array.Empty<FormDotRow>();
    private ThongTriDeclaration _declaration = new();
    private FormOrgInfo _org = new(null, null);
    private string _projectName = "";

    public ThongTriFormViewModel(IFormExcelExporter exporter, IDialogService dialog)
    {
        _exporter = exporter;
        _dialog = dialog;
        Picker.SelectionChanged += (_, _) => Rebuild();
    }

    public FormDotPickerViewModel Picker { get; } = new(Fund.NSNN);

    public ObservableCollection<FormLineRow> Lines { get; } = new();

    public string TotalText { get; private set; } = "";
    public string TotalInWords { get; private set; } = "";

    /// Bắn khi người dùng sửa một ô nhập tay, để workbook đánh dấu bẩn.
    public event EventHandler? DeclarationChanged;

    public string? SoThongTri { get => _declaration.SoThongTri; set => SetField(_declaration.SoThongTri, value, v => _declaration.SoThongTri = v); }
    public string? NgayGhiSo { get => _declaration.NgayGhiSo; set => SetField(_declaration.NgayGhiSo, value, v => _declaration.NgayGhiSo = v); }
    public string? TenCTMT { get => _declaration.TenCTMT; set => SetField(_declaration.TenCTMT, value, v => _declaration.TenCTMT = v); }
    public string? MaSo { get => _declaration.MaSo; set => SetField(_declaration.MaSo, value, v => _declaration.MaSo = v); }
    public string? VeThangNam { get => _declaration.VeThangNam; set => SetField(_declaration.VeThangNam, value, v => _declaration.VeThangNam = v); }
    public string? TaiKhoan1 { get => _declaration.TaiKhoan1; set => SetField(_declaration.TaiKhoan1, value, v => _declaration.TaiKhoan1 = v); }
    public string? DonVi1 { get => _declaration.DonVi1; set => SetField(_declaration.DonVi1, value, v => _declaration.DonVi1 = v); }
    public string? SoChungTu1 { get => _declaration.SoChungTu1; set => SetField(_declaration.SoChungTu1, value, v => _declaration.SoChungTu1 = v); }
    public string? TaiKhoan2 { get => _declaration.TaiKhoan2; set => SetField(_declaration.TaiKhoan2, value, v => _declaration.TaiKhoan2 = v); }
    public string? DonVi2 { get => _declaration.DonVi2; set => SetField(_declaration.DonVi2, value, v => _declaration.DonVi2 = v); }
    public string? SoChungTu2 { get => _declaration.SoChungTu2; set => SetField(_declaration.SoChungTu2, value, v => _declaration.SoChungTu2 = v); }
    public string? LoaiNganSach { get => _declaration.LoaiNganSach; set => SetField(_declaration.LoaiNganSach, value, v => _declaration.LoaiNganSach = v); }
    public string? Khoan { get => _declaration.Khoan; set => SetField(_declaration.Khoan, value, v => _declaration.Khoan = v); }
    public string? Muc { get => _declaration.Muc; set => SetField(_declaration.Muc, value, v => _declaration.Muc = v); }
    public string? TieuMuc { get => _declaration.TieuMuc; set => SetField(_declaration.TieuMuc, value, v => _declaration.TieuMuc = v); }
    public string? TietMuc { get => _declaration.TietMuc; set => SetField(_declaration.TietMuc, value, v => _declaration.TietMuc = v); }
    public string? Nganh { get => _declaration.Nganh; set => SetField(_declaration.Nganh, value, v => _declaration.Nganh = v); }

    /// Nạp lại từ cây hiện tại (kể cả phần chưa lưu) và từ bản khai đã lưu.
    public void Refresh(
        IReadOnlyList<FormDotRow> rows,
        IReadOnlyList<LedgerRowViewModel> sources,
        ThongTriDeclaration declaration,
        FormOrgInfo org,
        string projectName)
    {
        _rows = rows;
        _declaration = declaration;
        _org = org;
        _projectName = projectName;

        Picker.Refresh(rows, sources);
        RaiseAllDeclarationProperties();
        Rebuild();
    }

    /// Bản khai hiện tại để workbook lưu kèm cây.
    public ThongTriDeclaration ToDeclaration() => _declaration;

    private void Rebuild()
    {
        var form = ThongTriBuilder.Build(_rows, Picker.SelectedKeys, _declaration, _org, _projectName);

        Lines.Clear();
        foreach (var line in form.Lines)
            Lines.Add(new FormLineRow
            {
                Indent = line.Level,
                Content = line.Content,
                AmountText = MoneyText.Format(line.Amount),
                IsBold = line.Level == 0
            });

        TotalText = MoneyText.Format(form.Total);
        TotalInWords = form.TotalInWords;
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(TotalInWords));
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var suggested = $"ThongTri_{FileNames.Sanitize(_projectName)}_{DateTime.Now:yyyyMMdd}.xlsx";
        var path = await _dialog.PickSaveFileAsync("Xuất Thông tri", suggested, new[] { "xlsx" });
        if (path is null) return;

        try
        {
            var form = ThongTriBuilder.Build(_rows, Picker.SelectedKeys, _declaration, _org, _projectName);
            await _exporter.ExportThongTriAsync(form, path);
            await _dialog.ShowMessageAsync("Xuất Excel", $"Đã xuất Thông tri ra:\n{path}");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Xuất Thông tri thất bại");
            await _dialog.ShowMessageAsync("Lỗi", $"Không xuất được Thông tri: {ex.Message}");
        }
    }

    private void SetField(string? current, string? value, Action<string?> assign, [CallerMemberName] string? name = null)
    {
        if (current == value) return;
        assign(value);
        OnPropertyChanged(name);
        Rebuild();
        DeclarationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseAllDeclarationProperties()
    {
        foreach (var name in new[]
                 {
                     nameof(SoThongTri), nameof(NgayGhiSo), nameof(TenCTMT), nameof(MaSo), nameof(VeThangNam),
                     nameof(TaiKhoan1), nameof(DonVi1), nameof(SoChungTu1), nameof(TaiKhoan2), nameof(DonVi2),
                     nameof(SoChungTu2), nameof(LoaiNganSach), nameof(Khoan), nameof(Muc), nameof(TieuMuc),
                     nameof(TietMuc), nameof(Nganh)
                 })
            OnPropertyChanged(name);
    }
}
