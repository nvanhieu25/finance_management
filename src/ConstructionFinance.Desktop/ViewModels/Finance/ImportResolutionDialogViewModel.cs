using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.Import;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// One line of the import review list.
public partial class ImportResolutionRowVm : ObservableObject
{
    public ImportResolutionRowVm(ImportCandidate candidate)
    {
        Candidate = candidate;
        Name = candidate.Parsed.Name;
        IsDuplicate = candidate.ExistingId is not null;
        _resolution = candidate.Resolution;
    }

    public ImportCandidate Candidate { get; }
    public string Name { get; }
    public bool IsDuplicate { get; }

    public string StatusText => IsDuplicate ? "Đã có trong app" : "Dự án mới";

    /// Only duplicates get a choice; a new project has nothing to overwrite.
    public IReadOnlyList<ImportResolution> Options { get; } =
        new[] { ImportResolution.Overwrite, ImportResolution.Skip, ImportResolution.AddNew };

    [ObservableProperty] private ImportResolution _resolution;
}

/// Lets the user decide what happens to each imported project before anything is
/// written — the old importer appended blindly, which is how the legacy table
/// ended up holding the same five projects three times.
public partial class ImportResolutionDialogViewModel : ViewModelBase
{
    public ImportResolutionDialogViewModel(IReadOnlyList<ImportCandidate> candidates)
    {
        foreach (var candidate in candidates)
            Rows.Add(new ImportResolutionRowVm(candidate));
    }

    public ObservableCollection<ImportResolutionRowVm> Rows { get; } = new();

    public int DuplicateCount => Rows.Count(r => r.IsDuplicate);
    public int NewCount => Rows.Count(r => !r.IsDuplicate);

    public string Headline =>
        $"Tìm thấy {Rows.Count} dự án: {NewCount} mới, {DuplicateCount} trùng tên.";

    /// Fold the per-row choices back into candidates for the service. A row with
    /// nothing to overwrite keeps ExistingId null so the service adds it instead.
    public IReadOnlyList<ImportCandidate> ToCandidates() =>
        Rows.Select(r => r.Candidate with { Resolution = r.Resolution }).ToList();
}
