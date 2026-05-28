namespace ConstructionFinance.Infrastructure.Backup;

public record BackupFileInfo(
    string FullPath,
    string FileName,
    DateTime CreatedAtUtc,
    long SizeBytes,
    string BackupType,
    string? Description);
