# Sửa vùng bấm của ô trong lưới Vị trí (đồng bộ như Excel) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Trong lưới Sổ theo Vị trí, bấm vào **bất kỳ điểm nào** trong một ô đều đưa con trỏ vào đúng ô đó, và biên ô của mọi loại dòng (Nhóm / Gói / Đợt) trùng khít với nhau và với header — giống hệt Excel.

**Architecture:** Đây là bản sửa lỗi thuần XAML trong một tệp View duy nhất (`VitriLedgerView.axaml`), cộng hai bộ test hồi quy chạy trên Avalonia Headless. Không đụng tới view-model, tầng Application hay Domain.

**Tech Stack:** .NET 8, C# 12, Avalonia 11.2.1, Avalonia.Headless (test-only), CommunityToolkit.Mvvm, xUnit + Shouldly.

**Spec:** Không có tệp spec riêng — phần “Chẩn đoán” dưới đây **chính là spec**, đã được kiểm chứng bằng đo đạc headless thật (dump `Bounds` + `InputHitTest`) trước khi viết kế hoạch này. Đọc hết phần đó trước khi làm Task 1.

---

## Chẩn đoán (đã kiểm chứng bằng đo đạc, KHÔNG phải suy đoán)

### Nguyên nhân gốc 1 — style rò vào bên trong template của ô

`VitriLedgerView.axaml` (khoảng dòng 127) có:

```xml
<Style Selector="ItemsControl#RowsHost ContentPresenter">
    <Setter Property="Padding" Value="0"/>
    <Setter Property="Margin" Value="0"/>
    <Setter Property="HorizontalContentAlignment" Value="Left"/>
    <Setter Property="HorizontalAlignment" Value="Left"/>
</Style>
```

Ý định là chỉnh **vùng chứa dòng** (container `ContentPresenter` mà `ItemsControl` tự sinh cho mỗi dòng). Nhưng selector hậu duệ này khớp **mọi** `ContentPresenter` nằm dưới `RowsHost`, kể cả `ContentPresenter` **bên trong ControlTemplate của từng `TextBox`/`ComboBox` ô**.

Hậu quả đo được (dòng Đợt, cột "Số CT" rộng 90px, cột tiền rộng 110–120px, ô đang trống):

```
col 5 TextBox bounds=886,0,90,34      ← control chiếm đủ 90px
     · ContentPresenter bounds=0,0,12,34  HA=Left HCA=Left
     · Border           bounds=0,0,12,34  bg=#b3ffffff   ← NỀN + VÙNG BẤM chỉ 12px
```

`HorizontalAlignment=Left` làm presenter co lại bằng bề rộng **nội dung chữ**; ô trống chỉ còn 12px (= padding 6+6). `TextBox` không tự vẽ nền — nền do `Border` trong template vẽ — nên phần còn lại của ô **không hit-test được**, cú bấm rơi xuyên xuống `Grid` của dòng. Bản đồ `InputHitTest` xác nhận: bấm ở x=82 trong cột 0..100 trả về `Grid`, không phải `TextBox`.

Đây chính là “phải click vào đúng 1 vùng rất nhỏ”.

Tác dụng phụ kèm theo: vì presenter co theo nội dung, `TextAlignment="Right"` của các ô tiền (`TextBox.gridCell.num`) **không có hiệu lực** — số đang bị canh trái. Sửa xong sẽ tự canh phải như thiết kế.

**Kiểm chứng phương án sửa:** thay style trên bằng `ItemsControl.ItemContainerTheme` (chỉ áp cho container, không rò vào template) → đo lại: mọi presenter bên trong ô đều `HA=Stretch` và rộng đúng bằng cột (100/90/120/110), container vẫn `HorizontalAlignment=Left` như cũ.

### Nguyên nhân gốc 2 — `Margin` thụt đầu dòng làm nở chính cột đó

Ô "Nội dung" thụt đầu dòng theo cấp bằng `Margin` trái:

| Dòng | Vị trí trong tệp | Hiện tại |
|---|---|---|
| Nhóm | dòng ~445 | `Margin="4,0,0,0"` |
| Gói | dòng ~517 | `Margin="20,0,0,0"` |
| Đợt | dòng ~591 | `Margin="36,0,0,0"` |

`Margin` cộng **ngoài** bề rộng mong muốn của control. `TextBox` này lại có `MinWidth` buộc bằng đúng bề rộng cột (340), nên bề rộng mong muốn = 340 + margin > 340, và cột `Grid` bị nở ra theo. Đo được:

| Loại dòng | bề rộng cột "Nội dung" | x của cột kế tiếp | tổng bề rộng dòng |
|---|---|---|---|
| Nhóm (margin 4) | 344 | 444 | 1884 |
| Gói (margin 20) | 360 | 460 | 1900 |
| Đợt (margin 36) | 376 | 476 | 1916 |
| header / lớp kẻ dọc | 340 | 440 | 1880 |

Nghĩa là **ba loại dòng có ba hệ toạ độ cột khác nhau**, và không dòng nào khớp với header lẫn lớp kẻ dọc overlay (lớp này bám `ColumnWidths` danh nghĩa). Người dùng nhìn thấy đường kẻ ở một chỗ nhưng ô thật lại nằm lệch 4–36px — bấm “trượt” sang ô bên cạnh.

**Kiểm chứng phương án sửa:** đổi `Margin` trái thành `Padding` trái (padding nằm **trong** bề rộng, bị `MinWidth=340` hấp thụ) → đo lại: cả ba loại dòng đều có biên cột giống hệt nhau và giống header: `0/100/440/610/740/850/940/1060/1180/1290/1400/1520/1640/1760`, tổng `1880`.

### Ngoài phạm vi

- Ô placeholder rỗng (`Border.gridCellBorder`, ví dụ cột Nhà thầu trên dòng Đợt) vẫn chỉ chọn dòng chứ không nhập được — đúng thiết kế hiện tại, không đổi trong lần này.
- Không đổi bề rộng cột mặc định, không đổi màu, không đổi chiều cao dòng.

## Global Constraints

- **100% free-license.** Không thêm gói NuGet mới.
- **Đặt tên:** biến cục bộ và tham số `camelCase`; kiểu, phương thức, thuộc tính `PascalCase`; field private `_camelCase`.
- Chữ hiển thị bằng tiếng Việt.
- **Avalonia bị ghim 11.2.1** — `Grid` chưa có `ColumnSpacing`/`RowSpacing` (cần 11.3+); dùng `Margin`/`Padding` trên từng con.
- **Không** dùng `TwoWay` + `StringFormat` trên ô `decimal`/`DateOnly` trong XAML (bài học StackOverflow 6A).
- **Chỉ được sửa** `src/ConstructionFinance.Desktop/Views/Finance/VitriLedgerView.axaml` và thêm test. **Không** sửa `Styles/Theme.axaml` (dùng chung với lưới 6A), không sửa view-model, không sửa `VitriLedgerView.axaml.cs`.
- Build: `dotnet build ConstructionFinance.sln`. Test: `dotnet test ConstructionFinance.sln`. Toàn bộ suite hiện có **606 test xanh** — kết thúc mỗi task phải vẫn xanh và **0 warning**.
- Mỗi task kết thúc bằng một commit riêng.

## File Structure

| Tệp | Thay đổi |
|---|---|
| `src/ConstructionFinance.Desktop/Views/Finance/VitriLedgerView.axaml` | Sửa (Task 1 và Task 2) |
| `tests/ConstructionFinance.Desktop.Tests/Finance/VitriLedgerCellLayoutTests.cs` | Tạo mới (Task 1, bổ sung ở Task 2) |

Test mới đặt cạnh `VitriLedgerViewTests.cs` đã có và **dùng lại** hạ tầng headless của nó: `HeadlessTestSession.Run(...)` trong `tests/ConstructionFinance.Desktop.Tests/Finance/HeadlessTestSupport.cs`.

---

### Task 1: Cả ô đều bấm được (sửa nguyên nhân gốc 1)

**Files:**
- Create: `tests/ConstructionFinance.Desktop.Tests/Finance/VitriLedgerCellLayoutTests.cs`
- Modify: `src/ConstructionFinance.Desktop/Views/Finance/VitriLedgerView.axaml` (style ở ~dòng 127; phần tử `<ItemsControl x:Name="RowsHost" ...>` ở ~dòng 401)

**Interfaces:**
- Consumes: `HeadlessTestSession.Run(Action)` (internal, cùng assembly test, file `HeadlessTestSupport.cs`); `VitriLedgerViewModel(ViTri)`; `LedgerColumnWidths` với các hằng chỉ số cột `Ngay=0, NoiDungIndex=1, …, TongDaGnIndex=13` và `ColumnCount=14`; `RowKind { Total, Group, Package, Entry }`.
- Produces: tệp test với các helper `CreateLedger`, `FindRowGrid`, `ColumnLeftOffsets` mà Task 2 dùng lại.

- [ ] **Step 1: Viết test thất bại**

Tạo `tests/ConstructionFinance.Desktop.Tests/Finance/VitriLedgerCellLayoutTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
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

    private static Ledger CreateLedger(ViTri viTri)
    {
        var vm = new VitriLedgerViewModel(viTri);
        var view = new VitriLedgerView { DataContext = vm };
        var window = new Window { Content = view, Width = 1900, Height = 900 };
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
}
```

- [ ] **Step 2: Chạy test để xác nhận nó THẤT BẠI**

Chạy: `dotnet test tests/ConstructionFinance.Desktop.Tests/ConstructionFinance.Desktop.Tests.csproj --filter "FullyQualifiedName~VitriLedgerCellLayoutTests"`

Kỳ vọng: **FAIL**. `Nen_cua_moi_o_nhap_lieu_phu_kin_be_rong_cot` báo bề rộng vùng bấm ~12px trong khi cột 90–340px (dòng Đợt/Gói). `Bam_sat_mep_phai...` báo `IsFocused` là `false`.

Nếu test PASS ngay từ đầu thì test sai — dừng lại và báo cáo, đừng sửa code sản phẩm.

- [ ] **Step 3: Sửa — chuyển style container thành `ItemContainerTheme`**

Trong `src/ConstructionFinance.Desktop/Views/Finance/VitriLedgerView.axaml`, **xoá** cả khối style rò (giữ nguyên comment giải thích, viết lại cho đúng nơi mới) — đoạn hiện có:

```xml
        <!-- Zero out the ItemsControl item container's own padding/margin so a row's
             Grid starts at the exact same x-offset as the header/TỔNG CỘNG Grids —
             otherwise a ContentPresenter's own theme padding silently shifts every
             row a few px right of the frozen bars above it. -->
        <Style Selector="ItemsControl#RowsHost ContentPresenter">
            <Setter Property="Padding" Value="0"/>
            <Setter Property="Margin" Value="0"/>
            <Setter Property="HorizontalContentAlignment" Value="Left"/>
            <Setter Property="HorizontalAlignment" Value="Left"/>
        </Style>
```

và **thêm** `ItemContainerTheme` ngay trong `ItemsControl` (thẻ mở hiện tại là `<ItemsControl x:Name="RowsHost" ItemsSource="{Binding Rows}">`, đặt khối này TRƯỚC `<ItemsControl.ItemTemplate>`):

```xml
                        <ItemsControl x:Name="RowsHost" ItemsSource="{Binding Rows}">
                            <!-- Chuẩn hoá padding/margin/canh lề của CONTAINER mỗi dòng, để Grid
                                 của dòng bắt đầu đúng cùng một toạ độ x với header/TỔNG CỘNG.
                                 BẮT BUỘC đặt ở ItemContainerTheme chứ KHÔNG phải một Style
                                 "ItemsControl#RowsHost ContentPresenter": selector hậu duệ đó
                                 khớp cả những ContentPresenter nằm TRONG ControlTemplate của
                                 từng TextBox/ComboBox ô, và HorizontalAlignment=Left làm nền
                                 (cũng là vùng hit-test) của ô co lại bằng bề rộng chữ — ô trống
                                 chỉ còn 12px bấm được, phần cột còn lại rơi xuyên xuống Grid.
                                 ItemContainerTheme chỉ áp cho container do ItemsControl sinh ra. -->
                            <ItemsControl.ItemContainerTheme>
                                <ControlTheme TargetType="ContentPresenter">
                                    <Setter Property="Padding" Value="0"/>
                                    <Setter Property="Margin" Value="0"/>
                                    <Setter Property="HorizontalContentAlignment" Value="Left"/>
                                    <Setter Property="HorizontalAlignment" Value="Left"/>
                                </ControlTheme>
                            </ItemsControl.ItemContainerTheme>
```

- [ ] **Step 4: Chạy lại test**

Chạy: `dotnet test tests/ConstructionFinance.Desktop.Tests/ConstructionFinance.Desktop.Tests.csproj --filter "FullyQualifiedName~VitriLedgerCellLayoutTests"`

Kỳ vọng: **PASS** cả 4 test (3 `Theory` case + 1 `Fact`).

- [ ] **Step 5: Chạy toàn bộ suite**

Chạy: `dotnet test ConstructionFinance.sln`

Kỳ vọng: tất cả xanh (606 test cũ + 4 test mới = 610), 0 warning. Nếu có test cũ đỏ, báo cáo nguyên văn thông báo lỗi — **không** nới lỏng assert của test cũ để né.

- [ ] **Step 6: Commit**

```bash
git add src/ConstructionFinance.Desktop/Views/Finance/VitriLedgerView.axaml \
        tests/ConstructionFinance.Desktop.Tests/Finance/VitriLedgerCellLayoutTests.cs
git commit -m "fix(desktop): cả ô lưới Vị trí đều bấm được — thôi rò style vào template ô"
```

---

### Task 2: Biên cột trùng khít giữa mọi loại dòng và header (sửa nguyên nhân gốc 2)

**Files:**
- Modify: `tests/ConstructionFinance.Desktop.Tests/Finance/VitriLedgerCellLayoutTests.cs` (thêm test vào lớp đã tạo ở Task 1)
- Modify: `src/ConstructionFinance.Desktop/Views/Finance/VitriLedgerView.axaml` (3 chỗ: ~dòng 445, ~517, ~591)

**Interfaces:**
- Consumes: helper `CreateLedger`, `FindRowGrid`, `ColumnLeftOffsets`, `BuildOneOfEachRowKindFixture` do Task 1 tạo trong cùng tệp test.
- Produces: không có gì cho task sau (đây là task cuối).

- [ ] **Step 1: Viết test thất bại**

Thêm test này vào **cuối lớp** `VitriLedgerCellLayoutTests` (ngay trước dấu `}` đóng lớp):

```csharp
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
```

- [ ] **Step 2: Chạy test để xác nhận nó THẤT BẠI**

Chạy: `dotnet test tests/ConstructionFinance.Desktop.Tests/ConstructionFinance.Desktop.Tests.csproj --filter "FullyQualifiedName~Bien_cot_cua_moi_loai_dong"`

Kỳ vọng: **FAIL** cả 3 case. Dòng Nhóm lệch 4px (cột 2 ở x=444 thay vì 440, bề rộng dòng 1884 thay vì 1880), dòng Gói lệch 20px (460 / 1900), dòng Đợt lệch 36px (476 / 1916).

- [ ] **Step 3: Sửa — thụt đầu dòng bằng `Padding` thay vì `Margin`**

Trong `src/ConstructionFinance.Desktop/Views/Finance/VitriLedgerView.axaml`, sửa đúng 3 dòng.

Dòng Nhóm (`LedgerRowTemplateSelector.Group`, ~dòng 445) — đổi:

```xml
                                                         Margin="4,0,0,0" Text="{Binding Name}"/>
```

thành:

```xml
                                                         Padding="10,4,6,4" Text="{Binding Name}"/>
```

Dòng Gói (`LedgerRowTemplateSelector.Package`, ~dòng 517) — đổi:

```xml
                                                         Margin="20,0,0,0" Text="{Binding Name}"/>
```

thành:

```xml
                                                         Padding="26,4,6,4" Text="{Binding Name}"/>
```

Dòng Đợt (`LedgerRowTemplateSelector.Entry`, ~dòng 591) — đổi:

```xml
                                                         Margin="36,0,0,0" Text="{Binding Content}"/>
```

thành:

```xml
                                                         Padding="42,4,6,4" Text="{Binding Content}"/>
```

Ba giá trị padding = mức thụt cũ (4 / 20 / 36) **cộng** 6px padding ngang mặc định của `TextBox.gridCell` trong `Theme.axaml`, nên chữ vẫn thụt đúng như trước; `4` và `6` còn lại là padding trên/dưới và phải, y hệt `Padding="6,4"` mặc định. Padding nằm **trong** bề rộng control và bị `MinWidth` (đã buộc bằng bề rộng cột) hấp thụ, nên cột không nở — khác hẳn `Margin`.

Đồng thời cập nhật comment của style `TextBox.overflowCell` (khối comment ngay trên `<Style Selector="TextBox.overflowCell">`, ~dòng 78–88) — thêm một câu vào cuối comment đó:

```
             Mức thụt đầu dòng theo cấp đặt bằng Padding TRÁI của chính ô (không phải
             Margin): Margin cộng NGOÀI bề rộng mong muốn nên nó nở cả cột Grid ra
             thêm đúng bằng mức thụt, khiến ba loại dòng có ba hệ toạ độ cột khác nhau
             và lệch hẳn với header lẫn lớp kẻ dọc.
```

- [ ] **Step 4: Chạy lại test**

Chạy: `dotnet test tests/ConstructionFinance.Desktop.Tests/ConstructionFinance.Desktop.Tests.csproj --filter "FullyQualifiedName~VitriLedgerCellLayoutTests"`

Kỳ vọng: **PASS** cả 7 test trong lớp.

- [ ] **Step 5: Chạy toàn bộ suite**

Chạy: `dotnet test ConstructionFinance.sln`

Kỳ vọng: tất cả xanh (606 cũ + 7 mới = 613), 0 warning.

- [ ] **Step 6: Commit**

```bash
git add src/ConstructionFinance.Desktop/Views/Finance/VitriLedgerView.axaml \
        tests/ConstructionFinance.Desktop.Tests/Finance/VitriLedgerCellLayoutTests.cs
git commit -m "fix(desktop): thụt đầu dòng bằng Padding để biên cột mọi loại dòng trùng header"
```

---

## Nghiệm thu bằng mắt (người điều phối làm, không phải subagent)

Sau khi cả hai task xanh, chạy app thật và chụp màn hình đối chiếu:

```bash
pkill -9 -f "ConstructionFinance.Desktop/bin/Debug" || true
dotnet build ConstructionFinance.sln
CONSTRUCTIONFINANCE_DB=/tmp/claude-1000/ledger-check.db DISPLAY=:1 \
  setsid dotnet src/ConstructionFinance.Desktop/bin/Debug/net8.0/ConstructionFinance.Desktop.dll
```

Cần thấy: đường kẻ dọc thẳng hàng qua cả ba loại dòng; bấm ở mép phải một ô tiền trống vẫn vào đúng ô; số tiền canh phải.
