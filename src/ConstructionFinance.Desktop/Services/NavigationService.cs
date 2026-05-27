namespace ConstructionFinance.Desktop.Services;

public class NavigationService : INavigationService
{
    private readonly Stack<object> _back = new();

    public object? CurrentContent { get; private set; }
    public bool CanGoBack => _back.Count > 0;
    public event EventHandler<object?>? CurrentContentChanged;

    public void NavigateTo(object viewModel)
    {
        if (CurrentContent is not null)
            _back.Push(CurrentContent);
        CurrentContent = viewModel;
        CurrentContentChanged?.Invoke(this, CurrentContent);
    }

    public void GoBack()
    {
        if (_back.Count == 0) return;
        CurrentContent = _back.Pop();
        CurrentContentChanged?.Invoke(this, CurrentContent);
    }

    public void Reset()
    {
        _back.Clear();
        CurrentContent = null;
        CurrentContentChanged?.Invoke(this, null);
    }
}
