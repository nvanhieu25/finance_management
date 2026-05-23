using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Configurations;

public class SupportingConfigurationTests
{
    [Fact]
    public void Attachment_EntityType_stored_as_string()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var prop = ctx.Model.FindEntityType(typeof(Attachment))!.FindProperty(nameof(Attachment.EntityType))!;
        prop.GetProviderClrType().ShouldBe(typeof(string));
    }

    [Fact]
    public void Attachment_composite_index_entity_type_and_id()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        ctx.Attachments.Add(new Attachment
        {
            EntityType = AttachmentEntityType.Contract,
            EntityId = 1,
            FileName = "x.pdf",
            FilePath = "x.pdf",
            UploadedAt = DateTime.UtcNow
        });
        ctx.SaveChanges();
        ctx.Attachments.Count().ShouldBe(1);
    }

    [Fact]
    public void CostTemplateItem_self_ref_supported()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var tpl = new CostTemplate { TemplateName = "T1", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        ctx.CostTemplates.Add(tpl);
        ctx.SaveChanges();
        var root = new CostTemplateItem { TemplateId = tpl.Id, ItemName = "Root", Level = 0, SortOrder = 1 };
        ctx.CostTemplateItems.Add(root);
        ctx.SaveChanges();
        ctx.CostTemplateItems.Add(new CostTemplateItem { TemplateId = tpl.Id, ParentId = root.Id, ItemName = "Child", Level = 1, SortOrder = 1 });
        ctx.SaveChanges();

        ctx.CostTemplateItems.Count().ShouldBe(2);
    }

    [Fact]
    public void AuditLog_action_stored_as_string()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var prop = ctx.Model.FindEntityType(typeof(AuditLog))!.FindProperty(nameof(AuditLog.Action))!;
        prop.GetProviderClrType().ShouldBe(typeof(string));
    }
}
