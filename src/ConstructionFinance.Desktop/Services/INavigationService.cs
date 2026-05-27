namespace ConstructionFinance.Desktop.Services;

public interface INavigationService
{
    object? CurrentContent { get; }
    bool CanGoBack { get; }
    event EventHandler<object?>? CurrentContentChanged;

    void NavigateTo(object viewModel);
    void GoBack();
    void Reset();
}
