namespace ConstructionFinance.Desktop.Common;

public static class AppPaths
{
    public const string AppFolderName = "ConstructionFinance";

    public static string LocalAppDataRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppFolderName);

    public static string DatabaseFile => Path.Combine(LocalAppDataRoot, "app.db");
    public static string AttachmentsFolder => Path.Combine(LocalAppDataRoot, "attachments");
    public static string LogsFolder => Path.Combine(LocalAppDataRoot, "logs");
    public static string BackupsFolder => Path.Combine(LocalAppDataRoot, "Backups");

    public static string ConnectionString =>
        $"DataSource={DatabaseFile};Cache=Shared;Foreign Keys=True";

    public static void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(LocalAppDataRoot);
        Directory.CreateDirectory(AttachmentsFolder);
        Directory.CreateDirectory(LogsFolder);
        Directory.CreateDirectory(BackupsFolder);
    }
}
