using System.ComponentModel;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// One "DA" page — wraps the single-project entry grid. The tab caption follows
/// the project name, and the page mirrors the grid's unsaved state so the
/// workbook can show the bullet and drive Save-All.
public sealed class ProjectPageViewModel : WorkbookPageViewModel
{
    public ProjectPageViewModel(FinanceProjectGridViewModel grid)
    {
        Grid = grid;
        Title = DisplayName(grid.ProjectName);
        IsDirty = grid.IsDirty;

        Grid.DirtyChanged += (_, _) => IsDirty = Grid.IsDirty;
        Grid.PropertyChanged += OnGridPropertyChanged;
    }

    public FinanceProjectGridViewModel Grid { get; }

    public int ProjectId => Grid.CurrentProjectId;

    public override object Content => Grid;

    private void OnGridPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FinanceProjectGridViewModel.ProjectName))
            Title = DisplayName(Grid.ProjectName);
    }

    private static string DisplayName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "(dự án mới)" : name.Trim();
}
