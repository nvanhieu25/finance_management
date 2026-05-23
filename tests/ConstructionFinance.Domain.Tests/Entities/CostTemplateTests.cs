using ConstructionFinance.Domain.Entities;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Entities;

public class CostTemplateTests
{
    [Fact]
    public void CostTemplate_has_items_collection()
    {
        var tpl = new CostTemplate { TemplateName = "Công trình dân dụng" };
        tpl.Items.ShouldNotBeNull();
        tpl.Items.ShouldBeEmpty();
    }

    [Fact]
    public void CostTemplateItem_supports_self_ref()
    {
        var item = new CostTemplateItem
        {
            TemplateId = 1,
            ItemName = "Chi phí xây dựng",
            Level = 0,
            SortOrder = 1
        };

        item.ParentId.ShouldBeNull();
        item.Children.ShouldBeEmpty();
    }
}
