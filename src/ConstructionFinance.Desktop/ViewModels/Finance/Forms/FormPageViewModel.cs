using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Domain.Finance;
using Serilog;

namespace ConstructionFinance.Desktop.ViewModels.Finance.Forms;

/// Đầu mối mà lưới biểu mẫu (FormSheetControl) gọi khi người dùng gõ vào một ô.
public interface IFormSheetEditor
{
    void SetInput(string key, string? text);
    void ResetOverride(string key);

    /// Kéo biên cột/hàng: đặt bề rộng cột (ký tự Excel) hoặc chiều cao hàng (pt, theo khoá hàng của ResolvedSheet.RowKeys).
    void SetColumnWidth(int column, double chars);
    void SetRowHeight(string rowKey, double points);
}

public sealed record OrgEditedEventArgs(string Key, string? Value);

/// Nội dung của MỘT tab biểu mẫu — dùng chung cho Thông tri, KS chi và GĐNTT (khác nhau ở FormDefinition).
/// Không tự tính con số nào: mọi thứ đi qua FormDefinition.Build → builder có sẵn → FormSheetResolver.
public sealed partial class FormPageViewModel : ViewModelBase, IFormSheetEditor
{
    private readonly IFormExcelExporter _exporter;
    private readonly IDialogService _dialog;
    private readonly Dictionary<string, SegmentKind> _kinds;

    private IReadOnlyList<FormDotRow> _rows = Array.Empty<FormDotRow>();
    private FormInputSet _inputs = new();
    private FormOrgInfo _org = new(null, null);
    private string _projectName = "";

    public FormPageViewModel(FormDefinition definition, IFormExcelExporter exporter, IDialogService dialog)
    {
        Definition = definition;
        _exporter = exporter;
        _dialog = dialog;
        _kinds = definition.Template.AllSegments()
            .Where(segment => segment.Key is not null)
            .GroupBy(segment => segment.Key!)
            .ToDictionary(group => group.Key, group => group.First().Kind);

        Picker = new FormDotPickerViewModel(definition.FundFilter) { Title = $"Chọn đợt — {definition.Title}" };
        Picker.SelectionChanged += (_, _) => Rebuild();
        Rebuild();
    }

    public FormDefinition Definition { get; }

    public FormDotPickerViewModel Picker { get; }

    /// Năm điền vào chữ "năm …" của Mẫu. Đặt được để test không phụ thuộc đồng hồ.
    public int Year { get; set; } = DateTime.Now.Year;

    /// Tờ hiện tại. LUÔN khác null, kể cả khi GĐNTT đang bị chặn.
    public ResolvedSheet Sheet { get; private set; } = null!;

    public string? ErrorMessage { get; private set; }

    /// Vì sao "In toàn bộ" bỏ qua tab này (cụm viết thường, không dấu chấm cuối); null khi in được.
    public string? PrintBlockReason { get; private set; }

    public bool CanExport => ErrorMessage is null;
    public bool HasError => ErrorMessage is not null;
    public string ChooseDotsText => $"Chọn đợt ({Picker.SelectedCount})…";
    public string StatusText => ErrorMessage ?? Picker.SelectionSummary;

    /// Bắn khi một ô chữ/số của dự án này thật sự đổi — để workbook đánh dấu bẩn.
    public event EventHandler? InputsChanged;

    /// Bắn khi người dùng sửa tên đơn vị (lưu ở Cài đặt, không theo dự án) — workbook cập nhật mọi tab.
    public event EventHandler<OrgEditedEventArgs>? OrgEdited;

    // ===== nạp / lưu =====

    public void LoadInputs(IEnumerable<FormInput> stored)
    {
        _inputs = new FormInputSet(stored
            .Where(input => input.Form == Definition.Id)
            .Select(input => new KeyValuePair<string, string?>(input.Key, input.Value)));
        Rebuild();
    }

    public IEnumerable<FormInput> ToFormInputs() =>
        _inputs.Values
            .Where(pair => pair.Value is not null)
            .Select(pair => new FormInput { Form = Definition.Id, Key = pair.Key, Value = pair.Value });

    /// Nạp lại từ cây hiện tại (kể cả phần chưa lưu). KHÔNG đụng tới chữ đã gõ.
    public void Refresh(
        IReadOnlyList<FormDotRow> rows,
        IReadOnlyList<LedgerRowViewModel> sources,
        FormOrgInfo org,
        string projectName)
    {
        _rows = rows;
        _org = org;
        _projectName = projectName;
        Picker.Refresh(rows, sources);
        Rebuild();
    }

    // ===== gõ chữ =====

    public void SetInput(string key, string? text)
    {
        SegmentKind kind;
        if (FormSheetResolver.IsFreeKey(key)) kind = SegmentKind.Input;
        else if (!_kinds.TryGetValue(FormSheetResolver.TemplateKey(key), out kind)) return;
        text ??= "";

        var changed = kind switch
        {
            SegmentKind.SettingInput => SetOrg(key, text),
            SegmentKind.InputNumber => SetNumber(key, text),
            SegmentKind.Overridable => SetOverridable(key, text),
            _ => SetText(key, text),
        };
        if (!changed) return;

        Rebuild();
        if (kind != SegmentKind.SettingInput) InputsChanged?.Invoke(this, EventArgs.Empty);
    }

    // ===== cỡ ô =====

    /// Có ô nào đã bị kéo khỏi cỡ của Mẫu không (để nút "Đặt lại cỡ ô" sáng).
    public bool HasCustomLayout => _inputs.Values.Keys.Any(FormLayoutKeys.IsLayoutKey);

    public void SetColumnWidth(int column, double chars) =>
        SetLayout(FormLayoutKeys.Width(column), FormLayoutKeys.ToStored(Math.Clamp(chars, FormLayoutKeys.MinWidth, FormLayoutKeys.MaxWidth)));

    public void SetRowHeight(string rowKey, double points) =>
        SetLayout(FormLayoutKeys.Height(rowKey), FormLayoutKeys.ToStored(Math.Clamp(points, FormLayoutKeys.MinHeight, FormLayoutKeys.MaxHeight)));

    private void SetLayout(string key, string stored)
    {
        if (_inputs.Get(key) == stored) return;
        _inputs.Set(key, stored);
        Rebuild();
        InputsChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(HasCustomLayout))]
    private void ResetLayout()
    {
        var keys = _inputs.Values.Keys.Where(FormLayoutKeys.IsLayoutKey).ToList();
        if (keys.Count == 0) return;
        foreach (var key in keys) _inputs.Remove(key);
        Rebuild();
        InputsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ResetOverride(string key)
    {
        if (!_inputs.Remove(key)) return;
        Rebuild();
        InputsChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool SetText(string key, string text)
    {
        // So với chữ ĐANG HIỆN (đã gõ hoặc điền sẵn): gõ lại đúng chữ đó không phải là một thay đổi.
        // Ô trống mà TextBox báo "" (lúc dựng, lúc đổi chữ mờ) cũng không phải một lần gõ — nếu không, mở tab là tab dơ.
        if (CurrentSegment(key)?.Value == text) return false;
        _inputs.Set(key, text);
        return true;
    }

    private bool SetOrg(string key, string text)
    {
        if ((_org.Get(key) ?? "") == text) return false;
        _org = _org.With(key, text);
        OrgEdited?.Invoke(this, new OrgEditedEventArgs(key, text));
        return true;
    }

    private bool SetNumber(string key, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return _inputs.Remove(key);
        if (!MoneyText.TryParse(text, out var number)) return false;      // rác: giữ giá trị cũ
        var stored = FormInputSet.ToStored(number);
        if (_inputs.Get(key) == stored) return false;
        _inputs.Set(key, stored);
        return true;
    }

    private bool SetOverridable(string key, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return _inputs.Remove(key);     // xoá hết = trở về tự động

        var automatic = AutomaticSegment(key);
        if (automatic?.Number is not null)
        {
            if (!MoneyText.TryParse(text, out var number)) return false;
            if (number == automatic.Number) return _inputs.Remove(key);
            var stored = FormInputSet.ToStored(number);
            if (_inputs.Get(key) == stored) return false;
            _inputs.Set(key, stored);
            return true;
        }

        if (text == automatic?.Text) return _inputs.Remove(key);              // gõ đúng giá trị tự động
        if (_inputs.Get(key) == text) return false;
        _inputs.Set(key, text);
        return true;
    }

    private ResolvedSegment? CurrentSegment(string key) =>
        Sheet.Cells.SelectMany(cell => cell.Segments).FirstOrDefault(segment => segment.Key == key);

    /// Đoạn sẽ hiện nếu KHÔNG có chữ gõ đè — dựng thử một lần với bản sao đã bỏ khoá đó.
    private ResolvedSegment? AutomaticSegment(string key)
    {
        var probe = new FormInputSet(_inputs.Values.Where(pair => pair.Key != key));
        return Definition.Build(Context(probe)).Sheet.Cells
            .SelectMany(cell => cell.Segments).FirstOrDefault(segment => segment.Key == key);
    }

    // ===== dựng lại =====

    private FormBuildContext Context(FormInputSet inputs) =>
        new(_rows, Picker.SelectedKeys, inputs, _org, _projectName, Year);

    private void Rebuild()
    {
        var result = Definition.Build(Context(_inputs));
        Sheet = result.Sheet;
        ErrorMessage = result.Error;
        PrintBlockReason = result.PrintBlockReason;

        foreach (var name in new[]
                 {
                     nameof(Sheet), nameof(ErrorMessage), nameof(PrintBlockReason), nameof(CanExport),
                     nameof(HasError), nameof(StatusText), nameof(ChooseDotsText), nameof(HasCustomLayout)
                 })
            OnPropertyChanged(name);
        ExportCommand.NotifyCanExecuteChanged();
        ResetLayoutCommand.NotifyCanExecuteChanged();
    }

    // ===== lệnh =====

    [RelayCommand]
    private async Task ChooseDotsAsync()
    {
        var snapshot = Picker.SaveSelection();
        var confirmed = await _dialog.ShowDialogAsync(Picker);
        if (confirmed != true) Picker.RestoreSelection(snapshot);
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        var suggested = $"{Definition.ExportPrefix}_{FileNames.Sanitize(_projectName)}_{DateTime.Now:yyyyMMdd}.xlsx";
        var path = await _dialog.PickSaveFileAsync(Definition.ExportDialogTitle, suggested, new[] { "xlsx" });
        if (path is null) return;

        try
        {
            await _exporter.ExportAsync(Sheet, path);
            await _dialog.ShowMessageAsync("Xuất Excel", $"Đã xuất {Definition.Title} ra:\n{path}");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Xuất {Form} thất bại", Definition.Title);
            await _dialog.ShowMessageAsync("Lỗi", $"Không xuất được {Definition.Title}: {ex.Message}");
        }
    }
}
