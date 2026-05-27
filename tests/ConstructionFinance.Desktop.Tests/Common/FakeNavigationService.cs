using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.Tests.Common;

public class FakeNavigationService : INavigationService
{
    public object? CurrentContent { get; private set; }
    public bool CanGoBack => NavigationHistory.Count > 0;
    public event EventHandler<object?>? CurrentContentChanged;

    public List<object> NavigationHistory { get; } = new();

    public void NavigateTo(object viewModel)
    {
        NavigationHistory.Add(viewModel);
        CurrentContent = viewModel;
        CurrentContentChanged?.Invoke(this, viewModel);
    }

    public void GoBack()
    {
        if (NavigationHistory.Count == 0) return;
        NavigationHistory.RemoveAt(NavigationHistory.Count - 1);
        CurrentContent = NavigationHistory.LastOrDefault();
        CurrentContentChanged?.Invoke(this, CurrentContent);
    }

    public void Reset()
    {
        NavigationHistory.Clear();
        CurrentContent = null;
        CurrentContentChanged?.Invoke(this, null);
    }
}
