using ConstructionFinance.Desktop.Common;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Common;

public class AppPathsTests
{
    [Fact]
    public void DatabaseFile_uses_env_override_when_set()
    {
        var prev = Environment.GetEnvironmentVariable("CONSTRUCTIONFINANCE_DB");
        try
        {
            Environment.SetEnvironmentVariable("CONSTRUCTIONFINANCE_DB", "/tmp/app-dev.db");
            AppPaths.DatabaseFile.ShouldBe("/tmp/app-dev.db");
            AppPaths.ConnectionString.ShouldContain("/tmp/app-dev.db");
        }
        finally { Environment.SetEnvironmentVariable("CONSTRUCTIONFINANCE_DB", prev); }
    }

    [Fact]
    public void DatabaseFile_defaults_under_localappdata_when_env_absent()
    {
        var prev = Environment.GetEnvironmentVariable("CONSTRUCTIONFINANCE_DB");
        try
        {
            Environment.SetEnvironmentVariable("CONSTRUCTIONFINANCE_DB", null);
            AppPaths.DatabaseFile.ShouldEndWith("app.db");
        }
        finally { Environment.SetEnvironmentVariable("CONSTRUCTIONFINANCE_DB", prev); }
    }
}
