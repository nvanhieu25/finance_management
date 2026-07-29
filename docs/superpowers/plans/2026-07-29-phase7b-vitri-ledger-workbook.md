# Phase 7B — Vị trí ledger (outline grid) + workbook Dự án/Vị trí tabs

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development or superpowers:executing-plans. Steps use `- [ ]`.
> **STATUS: DRAFT for user review.** Written autonomously after Phase 7A. The data + VM-logic tasks (7B.1–7B.4) are headless-testable and can be executed by subagents. The View tasks (7B.5–7B.6) render the Avalonia UI and REQUIRE the user to run the app (DISPLAY=:1) and confirm visually — subagents cannot see UI. Several interaction details are marked **[UX — confirm with user]** and should be settled before building the View tasks.

**Goal:** Let the user enter a project's data as an Excel-like *Sổ theo dõi* — one hierarchical outline grid per Vị trí (nhóm Chi phí → Gói thầu → Đợt with frozen TỔNG CỘNG and live subtotals) — inside the existing workbook, with a Dự án picker on top and a Vị trí tab strip below.

**Architecture:** Reuse the 6A workbook shell (`FinanceWorkbookViewModel`, `WorkbookPageViewModel`, custom grid, IsDirty/Save-All). Replace the per-project *flat* grid with a per-Vị-trí *outline* grid backed by a flattened row model (`LedgerRowViewModel` with a `RowKind`), computing subtotals live via `TreeCalculator` (from 7A). Persistence uses a new `ReplaceTreeAsync` that saves a whole Project tree wholesale (mirrors 6A's replace strategy for lines).

**Tech Stack:** .NET 8, Avalonia 11, CommunityToolkit.Mvvm, EF Core 8, xUnit + Shouldly.

## Global Constraints

- 100% free-license deps only. Custom grid (no DataGrid/TreeDataGrid for editable cells).
- **6A runtime lessons (hard-won — do not relitigate):** (1) never combine TwoWay binding + StringFormat on a decimal/DateOnly-typed source (StackOverflow) — bind money/date to a **string facade**; (2) gate FluentAvalonia nav events behind real user input; (3) ContextMenu in a TabControl ItemTemplate doesn't open on FA → use toolbar buttons; (4) verify UI via `dotnet run` on DISPLAY=:1 with `_NET_WM_STATE_ABOVE` (don't move the window) + `mss` screenshots.
- Money displayed grouped by "." every 3 digits (10.000.000); typed raw, formatted on commit, parsed by stripping ".".
- Additive strangler continues: the old flat `FinanceProjectGridViewModel`/`LineItemRowViewModel`/`FinanceProjectGridView` stay until Phase 7E. Build the new outline grid alongside; swap what the workbook page hosts.
- All new calc reuses `TreeCalculator` (7A). Never persist derived figures.
- Namespaces: VMs `ConstructionFinance.Desktop.ViewModels.Finance`; money/parse helpers extend `Desktop.Common.FinanceCellParsing`.

## File Structure

- New `src/.../Application/Services/...` + `Data/Repositories/FinanceProjectRepository.cs`: `ReplaceTreeAsync(Project)` (save whole tree wholesale).
- New `Desktop/ViewModels/Finance/LedgerRowViewModel.cs`: one flattened grid row + `RowKind {Total, Group, Package, Entry}`; holds string facades for editable cells; raises `InputChanged`.
- New `Desktop/ViewModels/Finance/VitriLedgerViewModel.cs`: owns the flattened `ObservableCollection<LedgerRowViewModel>` for ONE Vị trí; live subtotals via `TreeCalculator`; add/remove group/package/entry; flatten-from-tree and rebuild-tree-for-save; IsDirty.
- New `Desktop/ViewModels/Finance/MoneyText.cs` (or extend `FinanceCellParsing`): format/parse "." grouping.
- Modify `Desktop/ViewModels/Finance/FinanceWorkbookViewModel.cs` + `ProjectPageViewModel`/`WorkbookPageViewModel`: Dự án picker + per-project Vị trí tab strip hosting `VitriLedgerViewModel`s.
- New `Desktop/Views/Finance/VitriLedgerView.axaml(.cs)`: the outline grid with per-RowKind cell templates.
- Modify `Desktop/Views/Finance/FinanceWorkbookView.axaml`: Dự án selector + Vị trí tabs.

---

### Task 7B.1: `ReplaceTreeAsync` — persist a whole Project tree (data layer)

**Files:**
- Modify: `src/ConstructionFinance.Domain/Abstractions/IFinanceProjectRepository.cs`
- Modify: `src/ConstructionFinance.Data/Repositories/FinanceProjectRepository.cs`
- Modify: `src/ConstructionFinance.Application/Services/Interfaces/IFinanceProjectService.cs` + `Implementations/FinanceProjectService.cs`
- Test: `tests/ConstructionFinance.Data.Tests/Finance/FinanceProjectRepositoryReplaceTreeTests.cs`

**Interfaces produced:**
- `Task ReplaceTreeAsync(Project project, CancellationToken ct = default)` on repo + service. Semantics: for `project.Id == 0` insert the whole tree (cascade); otherwise load existing tree, remove all its ViTris (cascade wipes descendants), re-add the incoming ViTris/CostGroups/Packages/Dots as fresh rows, update Project scalar fields. Mirrors the 6A `UpdateAsync` replace strategy (single-user, small data, sidesteps identity matching).

- [ ] **Step 1: failing test** — build a 2-Vị-trí tree, `AddAsync`; then mutate (rename a group, add a Dot, drop a package), `ReplaceTreeAsync`; reload via `GetTreeAsync` and assert the new shape + that orphaned Dots/Packages are gone (count check). Use `TestDbContextFactory.CreateSqliteInMemory()` with a fresh verify-context on the shared connection.
- [ ] **Step 2:** run — RED.
- [ ] **Step 3:** implement `ReplaceTreeAsync` on repo (Include full tree; `RemoveRange(existing.ViTris)` then re-add from incoming; set scalar fields), delegate from service.
- [ ] **Step 4:** run — GREEN; then `dotnet test` full suite.
- [ ] **Step 5:** commit `feat(data): ReplaceTreeAsync — persist whole Vị trí tree wholesale`.

### Task 7B.2: `MoneyText` — "." thousand grouping format/parse

**Files:** New `Desktop/Common/MoneyText.cs` (or extend `FinanceCellParsing`); Test in `tests/ConstructionFinance.Desktop.Tests/...`.

**Interfaces produced:** `string MoneyText.Format(decimal)` → "10.000.000" (no decimals for đồng; **`0m` → "" (blank)** — user-confirmed, Excel-like); `bool MoneyText.TryParse(string?, out decimal)` stripping "." and spaces, empty/whitespace → `0m`; round-trips with `FinanceCellParsing` conventions.

- [ ] Failing test (table: 0, 1000, 10_000_000, "10.000.000", "  10000 ", negative, garbage) → RED → implement with `NumberFormatInfo` (group sep ".", no decimal) → GREEN → commit `feat(desktop): MoneyText "." thousand-group format/parse`.

### Task 7B.3: `LedgerRowViewModel` + `RowKind` (one flattened row)

**Files:** New `Desktop/ViewModels/Finance/LedgerRowViewModel.cs`; Test `tests/ConstructionFinance.Desktop.Tests/Finance/LedgerRowViewModelTests.cs`.

**Interfaces produced:**
- `enum RowKind { Total, Group, Package, Entry }`
- `LedgerRowViewModel`: `RowKind Kind`; `int Depth`; for Entry rows — `DateText`/`Content`/`DocumentNo`/`FundText`(NSNN|Ngoại hối)/`AdvanceText`/`KlhtText`/`RecoveredText` **string facades** (money via `MoneyText`, date via string facade per 6A lesson); for Package rows — `Name`/`ContractorName`/`BankAccount`/`ContractValueText`/`ContractAnnexText`; for Group rows — `Name`; read-only computed display props for subtotal columns (set by the parent VM); `event EventHandler InputChanged` fired on any editable-facade change; `Dot ToDot()`/`Package header fields`/`Group name` extraction for save. **[UX — confirm: Fund entered as a dropdown (NSNN/Ngoại hối) — yes per spec]**.

- [ ] Failing tests: editing `AdvanceText="10.000.000"` exposes `Advance == 10_000_000m` and raises `InputChanged`; a Group row ignores money facades; `FundText` maps to `Fund` enum both ways. → RED → implement → GREEN → commit.

### Task 7B.4: `VitriLedgerViewModel` — flatten, live subtotals, edit tree (headless)

**Files:** New `Desktop/ViewModels/Finance/VitriLedgerViewModel.cs`; Test `tests/ConstructionFinance.Desktop.Tests/Finance/VitriLedgerViewModelTests.cs`.

**Interfaces produced:**
- Ctor takes a `ViTri` (or builds a blank one). `ObservableCollection<LedgerRowViewModel> Rows` — a TOTAL row, then per Group: a Group row, then per Package: a Package row, then its Entry rows (ordered by SortOrder).
- Commands: `AddGroup`, `AddPackage(group)`, `AddEntry(package)`, `DeleteSelected` — mutate the flattened rows AND keep an internal parent/child map so `ToViTri()` can rebuild the tree.
- `RecomputeSubtotals()`: rebuild a transient `ViTri` from rows, call `TreeCalculator.AggregateViTri` / per-group `AggregateGroup` / per-package `AggregatePackageEntity`, and push the computed columns (GN NSNN, GN Ngoại hối, Số dư tạm ứng, Tổng đã GN, Quyết toán năm) onto the TOTAL/Group/Package rows. Reacts to any row's `InputChanged`.
- `ViTri ToViTri()` for save; `bool IsDirty`; `event DirtyChanged`.
- **Decided (user-confirmed):** every Package sits under a Chi phí group — `AddPackage` requires a target group; if the Vị trí has no group yet, `AddPackage`/`AddEntry` is a no-op (or the UI disables the button). Tests assert this guard.

- [ ] Failing tests (headless, no rendering): build a Vị trí with 1 group/1 package/2 đợt; assert `Rows` order and kinds; assert the Package row's Số dư tạm ứng == ΣAdvance−ΣRecovered and the TOTAL row == sum; `AddEntry` then edit its `AdvanceText` updates subtotals live; `ToViTri()` round-trips the tree back (shape + values). → RED → implement → GREEN → `dotnet test` → commit.

### Task 7B.5: Workbook wiring — Dự án picker + Vị trí tabs **[USER VISUAL REVIEW]**

**Files:** Modify `FinanceWorkbookViewModel.cs`, `ProjectPageViewModel`/`WorkbookPageViewModel`, `Views/Finance/FinanceWorkbookView.axaml`.

**Behavior:** top Dự án selector (dropdown or tab row) + Thêm/Đổi tên/Xóa dự án; for the selected project, a bottom tab strip of its Vị trí (each hosting a `VitriLedgerView`) plus Thêm/Xóa/Đổi tên Vị trí; Save-All persists every dirty project via `ReplaceTreeAsync`; keep IsDirty • dot, close-guard, and leave-dirty-workbook guard from 6A. **[UX — confirm: Dự án as dropdown vs top tab row; where the Tổng hợp/Quyết toán năm/form tabs sit relative to Vị trí tabs.]**

- [ ] Build incrementally; **user runs the app on DISPLAY=:1** and confirms: project switch, Vị trí add/switch, dirty dot, Save-All round-trip. Headless VM-logic steps get unit tests first; rendering/interaction is user-verified. Commit after user confirms each sub-behavior.

### Task 7B.6: `VitriLedgerView.axaml` — the outline grid **[USER VISUAL REVIEW]**

**Files:** New `Views/Finance/VitriLedgerView.axaml(.cs)`.

**Behavior:** custom grid (extend the `FinanceProjectGridView` patterns): frozen TỔNG CỘNG row; per-RowKind row templates (Group bold + indent, Package header with contract fields, Entry with editable cells + Fund dropdown); full gridlines; money cells show `MoneyText`; keyboard nav + Ctrl+V TSV paste adapted to Entry rows only; "＋ Thêm nhóm / ＋ Thêm gói / ＋ Thêm đợt" affordances. Apply every 6A runtime lesson (string facades, no TwoWay+StringFormat, toolbar buttons not ContextMenu).

- [ ] Build incrementally with the user running the app and reviewing rendering + interaction after each meaningful increment. Commit per confirmed increment.

## Self-review notes (author)

- Spec §5.1/§5.2 (workbook + outline grid) → 7B.5, 7B.6. §5 dirty/Save-All → 7B.1 + 7B.5. Number "." → 7B.2. Fund dropdown → 7B.3. Live subtotals + số dư → 7B.4 (reuses 7A `TreeCalculator`). Tổng hợp/Quyết toán năm/forms are 7C/7D.
- 7A gap closed: 7B.1 adds `ReplaceTreeAsync` (7A only loaded trees).
- Open **[UX — confirm]** items are flagged inline; settle them with the user before 7B.5/7B.6 so the View is built once.
