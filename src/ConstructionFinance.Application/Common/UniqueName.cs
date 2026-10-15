namespace ConstructionFinance.Application.Common;

/// Đặt tên không trùng: "DA1" → "DA1 (2)" → "DA1 (3)" … cho tới khi tên còn trống.
public static class UniqueName
{
    public static string NextFree(string baseName, ISet<string> taken)
    {
        var trimmed = baseName.Trim();
        if (!taken.Contains(trimmed)) return trimmed;

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{trimmed} ({suffix})";
            if (!taken.Contains(candidate)) return candidate;
        }
    }
}
