using System.IO.Compression;
using System.Text.Json;
using ConstructionFinance.Infrastructure.Backup;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Infrastructure.Tests;

public class BackupServiceTests : IDisposable
{
    private readonly string _workDir;
    private readonly string _dbPath;
    private readonly string _attachmentsDir;
    private readonly string _backupDir;

    public BackupServiceTests()
    {
        _workDir = Path.Combine(Path.GetTempPath(), $"cf-backup-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workDir);
        _dbPath = Path.Combine(_workDir, "app.db");
        _attachmentsDir = Path.Combine(_workDir, "attachments");
        _backupDir = Path.Combine(_workDir, "Backups");
        Directory.CreateDirectory(_attachmentsDir);
        Directory.CreateDirectory(_backupDir);

        // Create a fake "DB" file and one attachment
        File.WriteAllText(_dbPath, "FAKE DB CONTENT");
        Directory.CreateDirectory(Path.Combine(_attachmentsDir, "contract", "1"));
        File.WriteAllText(Path.Combine(_attachmentsDir, "contract", "1", "doc.pdf"), "FAKE PDF");
    }

    public void Dispose()
    {
        if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true);
    }

    private static BackupManifest SampleManifest() => new(
        FormatVersion: "1.0",
        AppVersion: "1.0.0",
        DbSchemaVersion: "20260519_001",
        CreatedAtUtc: DateTime.UtcNow,
        CreatedBy: new BackupCreator("TEST-PC", "tester", "TestOS"),
        DataSummary: new BackupDataSummary(1, 1, 1, 10, 5, 2, 1, 100),
        DbSizeBytes: 1024,
        BackupType: "manual",
        Description: "test",
        CompanyName: "Test Org");

    [Fact]
    public async Task CreateBackupAsync_produces_cfb_file_in_backup_dir()
    {
        var svc = new BackupService();
        var manifest = SampleManifest();

        var result = await svc.CreateBackupAsync(_dbPath, _attachmentsDir, _backupDir, manifest);

        result.FullPath.ShouldNotBeNullOrEmpty();
        File.Exists(result.FullPath).ShouldBeTrue();
        result.FileName.ShouldEndWith(".cfb");
        result.BackupType.ShouldBe("manual");
    }

    [Fact]
    public async Task CreateBackupAsync_zip_contains_required_entries()
    {
        var svc = new BackupService();
        var result = await svc.CreateBackupAsync(_dbPath, _attachmentsDir, _backupDir, SampleManifest());

        using var zip = ZipFile.OpenRead(result.FullPath);
        var entryNames = zip.Entries.Select(e => e.FullName).ToList();
        entryNames.ShouldContain("manifest.json");
        entryNames.ShouldContain("app.db");
        entryNames.ShouldContain("checksum.txt");
        entryNames.ShouldContain(e => e.StartsWith("attachments/"));
    }

    [Fact]
    public async Task CreateBackupAsync_checksum_matches_db_sha256()
    {
        var svc = new BackupService();
        var result = await svc.CreateBackupAsync(_dbPath, _attachmentsDir, _backupDir, SampleManifest());

        using var zip = ZipFile.OpenRead(result.FullPath);
        var checksumEntry = zip.GetEntry("checksum.txt");
        checksumEntry.ShouldNotBeNull();
        using var reader = new StreamReader(checksumEntry!.Open());
        var actualChecksum = (await reader.ReadToEndAsync()).Trim();

        // Compute expected
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var fs = File.OpenRead(_dbPath);
        var expected = Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
        actualChecksum.ShouldBe(expected);
    }

    [Fact]
    public async Task CreateBackupAsync_manifest_round_trips_through_json()
    {
        var svc = new BackupService();
        var inputManifest = SampleManifest();
        var result = await svc.CreateBackupAsync(_dbPath, _attachmentsDir, _backupDir, inputManifest);

        using var zip = ZipFile.OpenRead(result.FullPath);
        var manifestEntry = zip.GetEntry("manifest.json")!;
        using var reader = new StreamReader(manifestEntry.Open());
        var json = await reader.ReadToEndAsync();
        var parsed = JsonSerializer.Deserialize<BackupManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        parsed.ShouldNotBeNull();
        parsed!.AppVersion.ShouldBe("1.0.0");
        parsed.DataSummary.CostItems.ShouldBe(10);
    }

    [Fact]
    public void ListBackupsAsync_returns_cfb_files_sorted_by_created_desc()
    {
        // Pre-create 2 fake .cfb files with different mtimes
        var oldPath = Path.Combine(_backupDir, "backup_20260101_120000_manual.cfb");
        var newPath = Path.Combine(_backupDir, "backup_20260520_120000_manual.cfb");
        File.WriteAllText(oldPath, "");
        File.WriteAllText(newPath, "");
        File.SetLastWriteTimeUtc(oldPath, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(newPath, new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc));

        var svc = new BackupService();
        var list = svc.ListBackups(_backupDir);

        list.Count.ShouldBe(2);
        list[0].FullPath.ShouldBe(newPath); // newest first
    }
}
