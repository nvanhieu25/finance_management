namespace ConstructionFinance.Infrastructure.Backup;

public interface IBackupService
{
    Task<BackupFileInfo> CreateBackupAsync(
        string sourceDbPath,
        string attachmentsDir,
        string backupDir,
        BackupManifest manifest,
        CancellationToken ct = default);

    IReadOnlyList<BackupFileInfo> ListBackups(string backupDir);
}
