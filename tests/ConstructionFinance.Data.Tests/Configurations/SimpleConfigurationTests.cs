using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Configurations;

public class SimpleConfigurationTests
{
    [Fact]
    public void Vendor_TaxCode_unique_when_not_null()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        ctx.Vendors.Add(new Vendor { VendorName = "A", TaxCode = "1111" });
        ctx.SaveChanges();

        ctx.Vendors.Add(new Vendor { VendorName = "B", TaxCode = "1111" });
        Should.Throw<DbUpdateException>(() => ctx.SaveChanges());
    }

    [Fact]
    public void Vendor_VendorName_max_length_255()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var prop = ctx.Model.FindEntityType(typeof(Vendor))!.FindProperty(nameof(Vendor.VendorName))!;
        prop.GetMaxLength().ShouldBe(255);
        prop.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void CostUnitType_Code_is_unique()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        ctx.CostUnitTypes.Add(new CostUnitType { Code = "m2", Name = "Mét vuông" });
        ctx.SaveChanges();

        ctx.CostUnitTypes.Add(new CostUnitType { Code = "m2", Name = "Other" });
        Should.Throw<DbUpdateException>(() => ctx.SaveChanges());
    }

    [Fact]
    public void AppSetting_Key_is_primary_key()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var pk = ctx.Model.FindEntityType(typeof(AppSetting))!.FindPrimaryKey()!;
        pk.Properties.Single().Name.ShouldBe(nameof(AppSetting.Key));
    }
}
