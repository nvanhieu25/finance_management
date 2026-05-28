using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace ConstructionFinance.Infrastructure.Backup;

public class RestoreService : IRestoreService
{
    public async Task<BackupManifest?> ReadManifestAsync(string cfbPath, CancellationToken ct = default)
    {
        using var zip = ZipFile.OpenRead(cfbPath);
        var entry = zip.GetEntry("manifest.json");
        if (entry is null) return null;
        await using var stream = entry.Open();
        return await JsonSerializer.DeserializeAsync<BackupManifest>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);
    }

    public async Task<bool> VerifyChecksumAsync(string cfbPath, CancellationToken ct = default)
    {
        using var zip = ZipFile.OpenRead(cfbPath);
        var checksumEntry = zip.GetEntry("checksum.txt");
        var dbEntry = zip.GetEntry("app.db");
        if (checksumEntry is null || dbEntry is null) return false;

        string expected;
        await using (var reader = checksumEntry.Open())
        using (var sr = new StreamReader(reader))
            expected = (await sr.ReadToEndAsync(ct)).Trim();

        await using var dbStream = dbEntry.Open();
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(dbStream, ct);
        var actual = Convert.ToHexString(hash).ToLowerInvariant();

        return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
    }

    public async Task RestoreAsync(
        string cfbPath,
        string targetDbPath,
        string targetAttachmentsDir,
        string backupDir,
        string currentSchemaVersion,
        CancellationToken ct = default)
    {
        // 1. Read & validate manifest
        var manifest = await ReadManifestAsync(cfbPath, ct)
            ?? throw new InvalidOperationException("Manifest không hợp lệ.");
        if (string.Compare(manifest.DbSchemaVersion, currentSchemaVersion, StringComparison.Ordinal) > 0)
            throw new InvalidOperationException(
                $"File backup có schema mới hơn ({manifest.DbSchemaVersion}) phiên bản app đang chạy ({currentSchemaVersion}). " +
                "Vui lòng cập nhật app trước khi restore.");

        // 2. Verify checksum
        if (!await VerifyChecksumAsync(cfbPath, ct))
            throw new InvalidOperationException("Checksum không khớp — file backup có thể bị hỏng.");

        // 3. Pre-restore safety backup of CURRENT state (if current DB exists)
        if (File.Exists(targetDbPath))
        {
            var safetyManifest = new BackupManifest(
                FormatVersion: "1.0",
                AppVersion: manifest.AppVersion,
                DbSchemaVersion: currentSchemaVersion,
                CreatedAtUtc: DateTime.UtcNow,
                CreatedBy: new BackupCreator(Environment.MachineName, Environment.UserName, Environment.OSVersion.ToString()),
                DataSummary: new BackupDataSummary(0, 0, 0, 0, 0, 0, 0, 0),
                DbSizeBytes: new FileInfo(targetDbPath).Length,
                BackupType: "pre_restore",
                Description: $"Auto safety backup before restoring {Path.GetFileName(cfbPath)}",
                CompanyName: manifest.CompanyName);
            await new BackupService().CreateBackupAsync(targetDbPath, targetAttachmentsDir, backupDir, safetyManifest, ct);
        }

        // 4. Atomic swap: extract to staging, then move into place
        var stagingDir = Path.Combine(Path.GetTempPath(), $"cf-restore-staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDir);
        try
        {
            ZipFile.ExtractToDirectory(cfbPath, stagingDir);

            // Replace DB
            var stagingDb = Path.Combine(stagingDir, "app.db");
            if (!File.Exists(stagingDb))
                throw new InvalidOperationException("Backup không chứa app.db.");
            if (File.Exists(targetDbPath)) File.Delete(targetDbPath);
            File.Move(stagingDb, targetDbPath);

            // Replace attachments — wipe existing dir + move from staging
            if (Directory.Exists(targetAttachmentsDir))
                Directory.Delete(targetAttachmentsDir, recursive: true);
            Directory.CreateDirectory(targetAttachmentsDir);
            var stagingAttachments = Path.Combine(stagingDir, "attachments");
            if (Directory.Exists(stagingAttachments))
            {
                CopyDirectory(stagingAttachments, targetAttachmentsDir);
            }
        }
        finally
        {
            if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, recursive: true);
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, dir)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)), overwrite: true);
    }
}
