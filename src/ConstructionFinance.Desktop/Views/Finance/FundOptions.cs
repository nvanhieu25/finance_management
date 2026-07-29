namespace ConstructionFinance.Desktop.Views.Finance;

/// Static option list for the Entry row's Nguồn (Fund) dropdown — bound via
/// {x:Static} so the ComboBox doesn't need an inline System.String item list
/// (fiddly to get right in Avalonia XAML) or a converter.
public static class FundOptions
{
    public static readonly string[] Names = { "NSNN", "Ngoại hối" };
}
