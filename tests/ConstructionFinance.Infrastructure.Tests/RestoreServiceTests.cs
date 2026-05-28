using ConstructionFinance.Infrastructure.Backup;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Infrastructure.Tests;

public class RestoreServiceTests : IDisposable
{
    private readonly string _workDir;
    private readonly string _dbPath;
    private readonly string _attachmentsDir;
    private readonly string _backupDir;

    public RestoreServiceTests()
    {
        _workDir = Path.Combine(Path.GetTempPath(), $"cf-restore-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workDir);
        _dbPath = Path.Combine(_workDir, "app.db");
        _attachmentsDir = Path.Combine(_workDir, "attachments");
        _backupDir = Path.Combine(_workDir, "Backups");
        Directory.CreateDirectory(_attachmentsDir);
        Directory.CreateDirectory(_backupDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true);
    }

    private async Task<string> CreateSampleBackup(string dbContent = "ORIGINAL", string schemaVersion = "20260519_001")
    {
        File.WriteAllText(_dbPath, dbContent);
        Directory.CreateDirectory(Path.Combine(_attachmentsDir, "contract", "1"));
        File.WriteAllText(Path.Combine(_attachmentsDir, "contract", "1", "x.pdf"), "PDF");
        var manifest = new BackupManifest(
            FormatVersion: "1.0",
            AppVersion: "1.0.0",
            DbSchemaVersion: schemaVersion,
            CreatedAtUtc: DateTime.UtcNow,
            CreatedBy: new BackupCreator("X", "y", "z"),
            DataSummary: new BackupDataSummary(0, 0, 0, 0, 0, 0, 0, 0),
            DbSizeBytes: dbContent.Length,
            BackupType: "manual",
            Description: null,
            CompanyName: null);
        var info = await new BackupService().CreateBackupAsync(_dbPath, _attachmentsDir, _backupDir, manifest);
        return info.FullPath;
    }

    [Fact]
    public async Task ReadManifestAsync_returns_manifest_for_valid_backup()
    {
        var path = await CreateSampleBackup();
        var svc = new RestoreService();

        var manifest = await svc.ReadManifestAsync(path);

        manifest.ShouldNotBeNull();
        manifest.AppVersion.ShouldBe("1.0.0");
    }

    [Fact]
    public async Task VerifyChecksumAsync_returns_true_when_valid()
    {
        var path = await CreateSampleBackup();
        (await new RestoreService().VerifyChecksumAsync(path)).ShouldBeTrue();
    }

    [Fact]
    public async Task RestoreAsync_replaces_db_and_attachments()
    {
        var path = await CreateSampleBackup(dbContent: "FROM_BACKUP");
        File.WriteAllText(_dbPath, "CURRENT"); // simulate current DB
        var svc = new RestoreService();

        await svc.RestoreAsync(path, _dbPath, _attachmentsDir, _backupDir, currentSchemaVersion: "20260519_001");

        File.ReadAllText(_dbPath).ShouldBe("FROM_BACKUP");
        File.Exists(Path.Combine(_attachmentsDir, "contract", "1", "x.pdf")).ShouldBeTrue();
    }

    [Fact]
    public async Task RestoreAsync_creates_pre_restore_safety_backup()
    {
        var path = await CreateSampleBackup();
        File.WriteAllText(_dbPath, "CURRENT_BEFORE_RESTORE");
        var svc = new RestoreService();

        await svc.RestoreAsync(path, _dbPath, _attachmentsDir, _backupDir, currentSchemaVersion: "20260519_001");

        var preRestoreFiles = Directory.GetFiles(_backupDir, "*pre_restore*.cfb");
        preRestoreFiles.Length.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task RestoreAsync_throws_when_backup_schema_newer_than_app()
    {
        var path = await CreateSampleBackup(schemaVersion: "20270101_999");
        File.WriteAllText(_dbPath, "CURRENT");
        var svc = new RestoreService();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await svc.RestoreAsync(path, _dbPath, _attachmentsDir, _backupDir, currentSchemaVersion: "20260519_001"));
    }
}
