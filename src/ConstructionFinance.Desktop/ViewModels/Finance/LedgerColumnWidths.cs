using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// Shared column-width model for the Vị trí ledger's outline grid (Excel-style manual
/// column resize). ONE instance is owned by a VitriLedgerViewModel and bound by the
/// header, the frozen TỔNG CỘNG row, and every RowKind row template, so all four grids
/// resize together the instant the user drags a header grip. Widths are always fixed
/// pixels (GridUnitType.Pixel) — never Auto/Star — dragging just changes the pixel value
/// here; the grid columns themselves stay plain fixed-width ColumnDefinitions bound to
/// these properties, so content can never grow/shift a column (only a drag can).
public partial class LedgerColumnWidths : ObservableObject
{
    public const double MinColumnWidth = 40;

    /// Visible left-to-right column order — used by the header's generic drag-grip
    /// handler, which only knows "column index N" (from the Thumb's Tag), not a property
    /// name.
    public const int Ngay = 0, NoiDungIndex = 1, NhaThauTkIndex = 2, GiaTriHdIndex = 3,
        PhuLucIndex = 4, SoCtIndex = 5, TamUngIndex = 6, KlhtIndex = 7, ThuHoiIndex = 8,
        NguonIndex = 9, GnNsnnIndex = 10, GnNgoaiHoiIndex = 11, SoDuTuIndex = 12, TongDaGnIndex = 13;

    public const int ColumnCount = 14;

    [ObservableProperty] private GridLength _ngayWidth = new(100);
    [ObservableProperty] private GridLength _noiDung = new(340);
    [ObservableProperty] private GridLength _nhaThauTk = new(170);
    [ObservableProperty] private GridLength _giaTriHd = new(130);
    [ObservableProperty] private GridLength _phuLuc = new(110);
    [ObservableProperty] private GridLength _soCt = new(90);
    [ObservableProperty] private GridLength _tamUng = new(120);
    [ObservableProperty] private GridLength _klht = new(120);
    [ObservableProperty] private GridLength _thuHoi = new(110);
    [ObservableProperty] private GridLength _nguon = new(110);
    [ObservableProperty] private GridLength _gnNsnn = new(120);
    [ObservableProperty] private GridLength _gnNgoaiHoi = new(120);
    [ObservableProperty] private GridLength _soDuTu = new(120);
    [ObservableProperty] private GridLength _tongDaGn = new(120);

    /// Reads the column at `index` (0..13, matching the grid's visible column order).
    public GridLength GetWidth(int index) => index switch
    {
        Ngay => NgayWidth,
        NoiDungIndex => NoiDung,
        NhaThauTkIndex => NhaThauTk,
        GiaTriHdIndex => GiaTriHd,
        PhuLucIndex => PhuLuc,
        SoCtIndex => SoCt,
        TamUngIndex => TamUng,
        KlhtIndex => Klht,
        ThuHoiIndex => ThuHoi,
        NguonIndex => Nguon,
        GnNsnnIndex => GnNsnn,
        GnNgoaiHoiIndex => GnNgoaiHoi,
        SoDuTuIndex => SoDuTu,
        TongDaGnIndex => TongDaGn,
        _ => default
    };

    private void SetWidth(int index, GridLength value)
    {
        switch (index)
        {
            case Ngay: NgayWidth = value; break;
            case NoiDungIndex: NoiDung = value; break;
            case NhaThauTkIndex: NhaThauTk = value; break;
            case GiaTriHdIndex: GiaTriHd = value; break;
            case PhuLucIndex: PhuLuc = value; break;
            case SoCtIndex: SoCt = value; break;
            case TamUngIndex: TamUng = value; break;
            case KlhtIndex: Klht = value; break;
            case ThuHoiIndex: ThuHoi = value; break;
            case NguonIndex: Nguon = value; break;
            case GnNsnnIndex: GnNsnn = value; break;
            case GnNgoaiHoiIndex: GnNgoaiHoi = value; break;
            case SoDuTuIndex: SoDuTu = value; break;
            case TongDaGnIndex: TongDaGn = value; break;
        }
    }

    /// Adds `deltaPixels` to the column at `index` (0..13), clamped to MinColumnWidth.
    /// Called by the header's drag grips; silently ignores an out-of-range index rather
    /// than throwing, so a stray/mistagged grip can never crash the UI thread mid-drag.
    public void Resize(int index, double deltaPixels)
    {
        if (index < 0 || index >= ColumnCount) return;
        var next = Math.Max(MinColumnWidth, GetWidth(index).Value + deltaPixels);
        SetWidth(index, new GridLength(next));
    }
}
