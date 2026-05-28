using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace ConstructionFinance.Infrastructure.Backup;

public class BackupService : IBackupService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public async Task<BackupFileInfo> CreateBackupAsync(
        string sourceDbPath,
        string attachmentsDir,
        string backupDir,
        BackupManifest manifest,
        CancellationToken ct = default)
    {
        if (!File.Exists(sourceDbPath))
            throw new FileNotFoundException("Source DB not found", sourceDbPath);
        Directory.CreateDirectory(backupDir);

        var stamp = manifest.CreatedAtUtc.ToString("yyyyMMdd_HHmmss");
        var fileName = $"backup_{stamp}_{manifest.BackupType}.cfb";
        var targetPath = Path.Combine(backupDir, fileName);

        // SHA256 of the DB file
        var checksum = await ComputeSha256Async(sourceDbPath, ct);

        await using (var zipStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            // 1. manifest.json
            var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
            await using (var w = new StreamWriter(manifestEntry.Open()))
            {
                await w.WriteAsync(JsonSerializer.Serialize(manifest, JsonOpts));
            }

            // 2. app.db
            archive.CreateEntryFromFile(sourceDbPath, "app.db", CompressionLevel.Optimal);

            // 3. checksum.txt
            var checksumEntry = archive.CreateEntry("checksum.txt", CompressionLevel.Optimal);
            await using (var w = new StreamWriter(checksumEntry.Open()))
            {
                await w.WriteAsync(checksum);
            }

            // 4. attachments/**
            if (Directory.Exists(attachmentsDir))
            {
                foreach (var file in Directory.EnumerateFiles(attachmentsDir, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(attachmentsDir, file).Replace(Path.DirectorySeparatorChar, '/');
                    archive.CreateEntryFromFile(file, $"attachments/{rel}", CompressionLevel.Optimal);
                }
            }
        }

        return new BackupFileInfo(
            FullPath: targetPath,
            FileName: fileName,
            CreatedAtUtc: manifest.CreatedAtUtc,
            SizeBytes: new FileInfo(targetPath).Length,
            BackupType: manifest.BackupType,
            Description: manifest.Description);
    }

    public IReadOnlyList<BackupFileInfo> ListBackups(string backupDir)
    {
        if (!Directory.Exists(backupDir)) return Array.Empty<BackupFileInfo>();
        return Directory.EnumerateFiles(backupDir, "*.cfb")
            .Select(p =>
            {
                var fi = new FileInfo(p);
                var name = fi.Name;
                var type = ParseBackupType(name);
                return new BackupFileInfo(
                    FullPath: p,
                    FileName: name,
                    CreatedAtUtc: fi.LastWriteTimeUtc,
                    SizeBytes: fi.Length,
                    BackupType: type,
                    Description: null);
            })
            .OrderByDescending(b => b.CreatedAtUtc)
            .ToList();
    }

    private static string ParseBackupType(string fileName)
    {
        // backup_YYYYMMDD_HHmmss_TYPE.cfb
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var parts = stem.Split('_');
        return parts.Length >= 4 ? parts[^1] : "unknown";
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        await using var fs = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(fs, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
