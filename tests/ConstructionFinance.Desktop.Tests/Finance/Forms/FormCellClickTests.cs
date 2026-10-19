using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Desktop.ViewModels.Finance.Forms;
using ConstructionFinance.Desktop.Views.Finance.Forms;
using ConstructionFinance.Domain.Finance;
using Moq;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace ConstructionFinance.Desktop.Tests.Finance.Forms;

/// Bấm CHUỘT THẬT (qua cửa sổ headless) vào nhiều điểm trong từng ô nhập của ba biểu mẫu: điểm bấm phải rơi đúng vào
/// ô đó và ô phải nhận tiêu điểm. Test hình học cũ không bắt được ô bị che bởi tay nắm kéo cỡ hay lớp khác.
public class FormCellClickTests
{
    private readonly ITestOutputHelper _output;
    public FormCellClickTests(ITestOutputHelper output) => _output = output;

    private static readonly FormOrgInfo Org = new("Sở Tài chính", "Ban QLDA số 1");

    private static FormDotRow Dot(FormDotKey key, Fund fund, decimal advance, string content) =>
        new(key, "Vị trí 1", "CHI PHÍ XÂY DỰNG", "Gói A", "Cty XL A", "123456789",
            50_000_000_000m, 0m, new DateOnly(2026, 3, 1), content, "CT01", fund, advance, 0m, 0m, advance);

    [Theory]
    [InlineData("thongtri")]
    [InlineData("kschi")]
    [InlineData("gdntt")]
    public void Every_input_cell_takes_focus_from_a_real_click_anywhere_inside_it(string id)
    {
        HeadlessTestSession.Run(() =>
        {
            var vm = new FormPageViewModel(FormDefinitions.All.Single(d => d.Id == id), Mock.Of<IFormExcelExporter>(), new FakeDialogService()) { Year = 2026 };
            vm.Refresh(new[]
            {
                Dot(new(0, 0, 0, 0), Fund.NSNN, 10_000_000m, "Tạm ứng lần 1"),
                Dot(new(0, 0, 0, 1), Fund.NSNN, 20_000_000m, "Thanh toán 1"),
                Dot(new(0, 0, 0, 2), Fund.NgoaiHoi, 30_000_000m, "Ngoại hối 1"),
            }, Array.Empty<LedgerRowViewModel>(), Org, "Dự án A");
            foreach (var item in vm.Picker.Items) item.IsSelected = true;

            var view = new FormPageView { DataContext = vm };
            var window = new Window { Content = view, Width = 1100, Height = 1400 };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var failures = new List<string>();
            var boxes = view.GetVisualDescendants().OfType<TextBox>().ToList();
            _output.WriteLine($"{id}: {boxes.Count} ô nhập");

            foreach (var box in boxes)
            {
                var origin = box.TranslatePoint(new Point(0, 0), window);
                if (origin is null) { failures.Add($"{box.Tag}: không có toạ độ"); continue; }
                var w = box.Bounds.Width; var h = box.Bounds.Height;
                var points = new (string Name, double X, double Y)[]
                {
                    ("giữa", w / 2, h / 2), ("trái-trên", 4, 4), ("phải-trên", w - 4, 4),
                    ("trái-dưới", 4, h - 4), ("phải-dưới", w - 4, h - 4),
                };
                foreach (var (name, x, y) in points)
                {
                    if (x < 0 || y < 0 || x > w || y > h) continue;
                    var p = new Point(origin.Value.X + x, origin.Value.Y + y);
                    var hit = window.InputHitTest(p) as Visual;
                    var ok = hit is not null && (ReferenceEquals(hit, box) || hit.GetVisualAncestors().Contains(box));
                    if (!ok)
                    {
                        failures.Add($"{box.Tag} [{name}] ({w:0}x{h:0}) trúng {hit?.GetType().Name ?? "null"}");
                        continue;
                    }
                    // bấm thật rồi xem ô có nhận tiêu điểm
                    view.Focus();
                    window.MouseDown(p, MouseButton.Left);
                    window.MouseUp(p, MouseButton.Left);
                    Dispatcher.UIThread.RunJobs();
                    if (!box.IsFocused && !box.GetVisualDescendants().OfType<Control>().Any(c => c.IsFocused))
                        failures.Add($"{box.Tag} [{name}] bấm xong không nhận tiêu điểm");
                }
            }

            window.Close();
            failures.ShouldBeEmpty(string.Join("\n", failures.Take(40)));
        });
    }
}
