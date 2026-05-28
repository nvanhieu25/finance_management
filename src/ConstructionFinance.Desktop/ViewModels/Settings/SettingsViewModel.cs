using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.Settings;

public class SettingsViewModel : ViewModelBase
{
    public SettingsViewModel(CompanyInfoViewModel company, BackupManagementViewModel backup, PreferencesViewModel preferences)
    {
        CompanyInfo = company;
        Backup = backup;
        Preferences = preferences;
    }

    public CompanyInfoViewModel CompanyInfo { get; }
    public BackupManagementViewModel Backup { get; }
    public PreferencesViewModel Preferences { get; }

    public async Task InitializeAsync()
    {
        await CompanyInfo.LoadAsync();
        await Backup.LoadAsync();
        await Preferences.LoadAsync();
    }
}
