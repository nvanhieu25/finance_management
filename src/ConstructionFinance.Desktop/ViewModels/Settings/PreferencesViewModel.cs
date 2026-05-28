using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.Settings;

public partial class PreferencesViewModel : ViewModelBase
{
    public const string KeyAutoBackupOnExit = "auto_backup_on_exit";

    private readonly IAppSettingService _settings;
    private readonly IDialogService _dialog;

    public PreferencesViewModel(IAppSettingService settings, IDialogService dialog)
    {
        _settings = settings;
        _dialog = dialog;
    }

    [ObservableProperty] private bool _autoBackupOnExit = true;

    public async Task LoadAsync()
    {
        var raw = await _settings.GetAsync(KeyAutoBackupOnExit);
        AutoBackupOnExit = raw is null || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task SaveAsync()
    {
        await _settings.SetAsync(KeyAutoBackupOnExit, AutoBackupOnExit ? "true" : "false");
    }
}
