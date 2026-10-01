using System.Diagnostics;
using Serilog;

namespace ConstructionFinance.Desktop.Services;

public interface IFileLauncher
{
    /// false nghĩa là hệ điều hành không mở được — người gọi hiện đường dẫn thay thế.
    bool TryOpen(string path);
}

public sealed class FileLauncher : IFileLauncher
{
    public bool TryOpen(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Không mở được tệp {Path}", path);
            return false;
        }
    }
}
