using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Desktop.Views.Finance;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

/// Lưới Vị trí phải hành xử như Excel: bấm vào BẤT KỲ điểm nào trong một ô đều đưa con
/// trỏ vào đúng ô đó, và biên ô của mọi loại dòng trùng khít nhau lẫn trùng header.
/// Hai lỗi được chốt lại ở đây đều là lỗi TẦNG BỐ CỤC — không test được từ view-model,
/// chỉ lộ ra khi đo Bounds thật của cây visual đã dựng xong.
public class VitriLedgerCellLayoutTests
{
    private sealed record Ledger(Window Window, VitriLedgerView View, VitriLedgerViewModel ViewModel);

    /// Một nhóm / một gói / một đợt — đủ chạm mọi loại dòng. Mọi ô tiền để 0m, mà
    /// MoneyText.Format(0m) trả "" nên mọi ô tiền đều TRỐNG: đó chính là trường hợp
    /// lỗi nặng nhất (nền của ô co lại bằng bề rộng nội dung, tức gần như bằng 0).
    private static ViTri BuildOneOfEachRowKindFixture()
    {
        var viTri = new ViTri { Name = "Vị trí test" };
        var group = new CostGroup { Name = "Nhóm 1", SortOrder = 0 };
        var package = new Package { Name = "Gói A", SortOrder = 0 };
        package.Dots.Add(new Dot { Content = "Đợt 1", SortOrder = 0, Fund = Fund.NSNN });
        group.Packages.Add(package);
        viTri.CostGroups.Add(group);
        return viTri;
        // Rows: [0] Total, [1] Nhóm 1, [2] Gói A, [3] Đợt 1
    }

    private static Ledger CreateLedger(ViTri viTri, double windowWidth = 1900)
    {
        var vm = new VitriLedgerViewModel(viTri);
        var view = new VitriLedgerView { DataContext = vm };
        var window = new Window { Content = view, Width = windowWidth, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return new Ledger(window, view, vm);
    }

    private static Grid FindRowGrid(VitriLedgerView view, int rowIndex)
    {
        var rowsHost = view.FindControl<ItemsControl>("RowsHost")!;
        var container = rowsHost.ContainerFromIndex(rowIndex) as Control
            ?? throw new InvalidOperationException($"Chưa dựng container cho dòng {rowIndex}.");
        return container.GetVisualDescendants().OfType<Grid>()
            .First(g => g.Classes.Contains("gridRow"));
    }

    /// Toạ độ trái danh nghĩa của 14 cột, cộng dồn từ LedgerColumnWidths — đây là hệ
    /// toạ độ mà header và lớp kẻ dọc overlay dùng, nên mọi dòng phải khớp với nó.
    private static double[] ColumnLeftOffsets(LedgerColumnWidths widths)
    {
        var offsets = new double[LedgerColumnWidths.ColumnCount];
        var x = 0d;
        for (var i = 0; i < LedgerColumnWidths.ColumnCount; i++)
        {
            offsets[i] = x;
            x += widths.GetWidth(i).Value;
        }
        return offsets;
    }

    /// Các ô nhập liệu (TextBox là con TRỰC TIẾP của Grid dòng), kèm chỉ số cột.
    private static IEnumerable<(int Column, TextBox Box)> EditableCells(Grid rowGrid) =>
        rowGrid.Children.OfType<TextBox>()
            .Where(t => Grid.GetColumnSpan(t) <= 1)
            .Select(t => (Grid.GetColumn(t), t));

    [Theory]
    [InlineData(1)] // Nhóm
    [InlineData(2)] // Gói
    [InlineData(3)] // Đợt
    public void Nen_cua_moi_o_nhap_lieu_phu_kin_be_rong_cot(int rowIndex)
    {
        HeadlessTestSession.Run(() =>
        {
            var (window, view, vm) = CreateLedger(BuildOneOfEachRowKindFixture());
            var rowGrid = FindRowGrid(view, rowIndex);

            foreach (var (column, box) in EditableCells(rowGrid))
            {
                var expected = vm.ColumnWidths.GetWidth(column).Value;

                // ContentPresenter trong ControlTemplate của TextBox là thứ đặt nền —
                // TextBox không tự vẽ nền, nên bề rộng presenter CHÍNH LÀ vùng bấm được.
                var presenter = box.GetVisualDescendants().OfType<ContentPresenter>().First();

                presenter.Bounds.Width.ShouldBe(expected, 0.5,
                    $"dòng {rowIndex} cột {column}: vùng bấm được rộng {presenter.Bounds.Width}px " +
                    $"nhưng cột rộng {expected}px");
            }

            window.Close();
        });
    }

    [Fact]
    public void Bam_sat_mep_phai_cua_o_tien_van_dua_con_tro_vao_o_do()
    {
        HeadlessTestSession.Run(() =>
        {
            var (window, view, vm) = CreateLedger(BuildOneOfEachRowKindFixture());
            var entryGrid = FindRowGrid(view, 3);
            var tamUng = entryGrid.Children.OfType<TextBox>()
                .First(t => Grid.GetColumn(t) == LedgerColumnWidths.TamUngIndex);

            // Sát mép phải của ô, giữa chiều cao — chỗ mà lỗi cũ để rơi xuyên xuống Grid.
            var target = tamUng.TranslatePoint(
                new Point(tamUng.Bounds.Width - 3, tamUng.Bounds.Height / 2), window)!.Value;

            window.MouseDown(target, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(target, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            tamUng.IsFocused.ShouldBeTrue("bấm sát mép phải ô Tạm ứng phải đưa con trỏ vào chính ô đó");
            window.Close();
        });
    }

    [Theory]
    [InlineData(1)] // Nhóm
    [InlineData(2)] // Gói
    [InlineData(3)] // Đợt
    public void Bien_cot_cua_moi_loai_dong_trung_voi_toa_do_danh_nghia_cua_header(int rowIndex)
    {
        HeadlessTestSession.Run(() =>
        {
            var (window, view, vm) = CreateLedger(BuildOneOfEachRowKindFixture());
            var rowGrid = FindRowGrid(view, rowIndex);
            var offsets = ColumnLeftOffsets(vm.ColumnWidths);

            // Bỏ qua các Border phủ toàn dòng (ColumnSpan=14: vạch kẻ ngang + lớp chọn dòng).
            foreach (var cell in rowGrid.Children.OfType<Control>().Where(c => Grid.GetColumnSpan(c) <= 1))
            {
                var column = Grid.GetColumn(cell);
                cell.Bounds.X.ShouldBe(offsets[column], 0.5,
                    $"dòng {rowIndex} cột {column} bắt đầu ở x={cell.Bounds.X} " +
                    $"thay vì {offsets[column]} như header/lớp kẻ dọc");
            }

            // Tổng bề rộng dòng phải đúng bằng tổng bề rộng 14 cột — không dòng nào
            // được tự nở thêm vì nội dung/lề của chính nó.
            var totalWidth = offsets[^1] + vm.ColumnWidths.GetWidth(LedgerColumnWidths.TongDaGnIndex).Value;
            rowGrid.Bounds.Width.ShouldBe(totalWidth, 0.5);

            window.Close();
        });
    }

    /// Khác với test bên trên (so dòng với toạ độ CỘNG DỒN từ LedgerColumnWidths), test này
    /// so dòng với vị trí RENDER THẬT của header và lớp kẻ dọc — đúng thứ mà 7 test cũ
    /// không hề chạm tới, nên một header/overlay lệch phải 13px vẫn lọt qua 7 test xanh.
    /// Viewport CỐ Ý rộng hơn hẳn 1880px (bề rộng danh nghĩa của bảng): header/TỔNG CỘNG/
    /// hai overlay kẻ dọc không có HorizontalAlignment riêng nên mặc định Stretch — khi
    /// viewport rộng hơn bảng, Avalonia rải phần dư cho các Grid này (mỗi cột +1px), còn
    /// Grid của từng DÒNG bị ItemContainerTheme ghim HorizontalAlignment=Left nên vẫn giữ
    /// đúng 1880px. Ở viewport <= ~1882px không có phần dư để rải nên test này PASS ngay cả
    /// khi còn lỗi — phải dùng viewport rộng hơn hẳn 1880 (ở đây 2400, cùng mức reviewer đã
    /// đo) mới thực sự bắt được lỗi.
    [Fact]
    public void Bien_cot_header_va_lop_ke_doc_trung_voi_bien_cot_thuc_te_cua_dong_khi_vien_rong_hon_bang()
    {
        HeadlessTestSession.Run(() =>
        {
            var (window, view, vm) = CreateLedger(BuildOneOfEachRowKindFixture(), windowWidth: 2400);

            var headerGrid = view.FindControl<Grid>("HeaderColumnsGrid")!;
            var rowsHost = view.FindControl<ItemsControl>("RowsHost")!;
            // Overlay kẻ dọc của TOÀN BỘ phần cuộn (không phải overlay của thanh TỔNG CỘNG):
            // trong VitriLedgerView.axaml nó là Grid duy nhất khác, anh em trực tiếp của
            // RowsHost, bên trong Grid bọc ngoài cùng của ScrollViewer thân bảng.
            var bodyDividerOverlay = rowsHost.GetVisualParent<Grid>()!.Children
                .OfType<Grid>().Single();

            for (var column = 0; column < LedgerColumnWidths.ColumnCount; column++)
            {
                var headerCell = headerGrid.Children.OfType<Grid>()
                    .Single(g => Grid.GetColumn(g) == column);
                var headerLeftEdge = headerCell.TranslatePoint(new Point(0, 0), window)!.Value.X;

                // Border.colDivider canh PHẢI (HorizontalAlignment=Right, Width=1), nên
                // Bounds của chính nó là mép phải trừ 1px chứ không phải mép trái của cột —
                // lấy điểm local (Bounds.Width, 0), tức mép phải thật, rồi quy đổi sang toạ
                // độ Window để so cho đúng chiều (mép phải với mép phải).
                var divider = bodyDividerOverlay.Children.OfType<Border>()
                    .Single(b => b.Classes.Contains("colDivider") && Grid.GetColumn(b) == column);
                var dividerRightEdge = divider.TranslatePoint(new Point(divider.Bounds.Width, 0), window)!.Value.X;

                foreach (var rowIndex in new[] { 1, 2, 3 }) // Nhóm, Gói, Đợt — đều phải khớp.
                {
                    var rowGrid = FindRowGrid(view, rowIndex);
                    var rowCell = rowGrid.Children.OfType<Control>()
                        .Single(c => Grid.GetColumn(c) == column && Grid.GetColumnSpan(c) <= 1);
                    var rowLeftEdge = rowCell.TranslatePoint(new Point(0, 0), window)!.Value.X;
                    var rowRightEdge = rowCell.TranslatePoint(new Point(rowCell.Bounds.Width, 0), window)!.Value.X;

                    headerLeftEdge.ShouldBe(rowLeftEdge, 0.5,
                        $"cột {column} dòng {rowIndex}: header ở x={headerLeftEdge} " +
                        $"nhưng dòng ở x={rowLeftEdge}");
                    dividerRightEdge.ShouldBe(rowRightEdge, 0.5,
                        $"cột {column} dòng {rowIndex}: lớp kẻ dọc ở x={dividerRightEdge} " +
                        $"nhưng mép phải ô của dòng ở x={rowRightEdge}");
                }
            }

            window.Close();
        });
    }
}
