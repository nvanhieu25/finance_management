using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Import;

/// What to do with one parsed project that collides with an existing one.
public enum ImportResolution
{
    Overwrite,
    Skip,
    AddNew
}

/// One parsed project plus the user's decision. ExistingId is null when the
/// project is new (nothing to collide with).
public record ImportCandidate(Project Parsed, int? ExistingId, ImportResolution Resolution);

/// Pure name-matching for legacy-Excel import. Kept free of EF so it can be
/// tested without a database — the old importer's silent duplicate-append is
/// exactly what this replaces.
public static class ImportMerge
{
    /// Pair each parsed project with an existing one by name (case/padding
    /// insensitive). Duplicates default to Overwrite — re-importing a workbook
    /// normally means "take the newer numbers"; the user can change each row.
    public static IReadOnlyList<ImportCandidate> Classify(
        IReadOnlyList<Project> parsed,
        IReadOnlyList<Project> existing)
    {
        var byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in existing)
            byName[Key(project.Name)] = project.Id;

        var candidates = new List<ImportCandidate>(parsed.Count);
        foreach (var project in parsed)
        {
            if (byName.TryGetValue(Key(project.Name), out var id))
                candidates.Add(new ImportCandidate(project, id, ImportResolution.Overwrite));
            else
                candidates.Add(new ImportCandidate(project, null, ImportResolution.AddNew));
        }
        return candidates;
    }

    /// "DA1" → "DA1 (2)" → "DA1 (3)" … until the name is free.
    public static string NextFreeName(string baseName, ISet<string> taken)
    {
        var trimmed = baseName.Trim();
        if (!taken.Contains(trimmed)) return trimmed;

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{trimmed} ({suffix})";
            if (!taken.Contains(candidate)) return candidate;
        }
    }

    private static string Key(string name) => name.Trim();
}
