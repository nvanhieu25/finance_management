namespace ConstructionFinance.Infrastructure.Backup;

public record BackupManifest(
    string FormatVersion,
    string AppVersion,
    string DbSchemaVersion,
    DateTime CreatedAtUtc,
    BackupCreator CreatedBy,
    BackupDataSummary DataSummary,
    long DbSizeBytes,
    string BackupType,
    string? Description,
    string? CompanyName);

public record BackupCreator(string MachineName, string UserName, string OsVersion);

public record BackupDataSummary(
    int ApprovalDecisions,
    int Contracts,
    int Projects,
    int CostItems,
    int Payments,
    int Vendors,
    int AttachmentsCount,
    long AttachmentsTotalSizeBytes);
