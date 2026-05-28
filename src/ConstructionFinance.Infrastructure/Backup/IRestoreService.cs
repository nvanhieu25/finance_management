namespace ConstructionFinance.Infrastructure.Backup;

public interface IRestoreService
{
    Task<BackupManifest?> ReadManifestAsync(string cfbPath, CancellationToken ct = default);
    Task<bool> VerifyChecksumAsync(string cfbPath, CancellationToken ct = default);
    Task RestoreAsync(
        string cfbPath,
        string targetDbPath,
        string targetAttachmentsDir,
        string backupDir,
        string currentSchemaVersion,
        CancellationToken ct = default);
}
