using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Entities;

public class SupportingEntitiesTests
{
    [Fact]
    public void Attachment_has_polymorphic_target()
    {
        var att = new Attachment
        {
            EntityType = AttachmentEntityType.Contract,
            EntityId = 42,
            FileName = "hd.pdf",
            FilePath = "contract/42/hd.pdf",
            FileSize = 12345,
            MimeType = "application/pdf"
        };

        att.EntityType.ShouldBe(AttachmentEntityType.Contract);
        att.EntityId.ShouldBe(42);
        att.FileName.ShouldBe("hd.pdf");
    }

    [Fact]
    public void AuditLog_records_change()
    {
        var log = new AuditLog
        {
            EntityType = "Project",
            EntityId = 1,
            Action = AuditAction.Update,
            FieldName = "ProjectName",
            OldValue = "Old",
            NewValue = "New",
            ChangedAt = new DateTime(2026, 5, 19, 14, 23, 0, DateTimeKind.Utc)
        };

        log.Action.ShouldBe(AuditAction.Update);
        log.OldValue.ShouldBe("Old");
        log.NewValue.ShouldBe("New");
    }

    [Fact]
    public void AppSetting_uses_string_key_as_pk()
    {
        var setting = new AppSetting
        {
            Key = "company_name",
            Value = "Sở Xây dựng"
        };

        setting.Key.ShouldBe("company_name");
        setting.Value.ShouldBe("Sở Xây dựng");
    }
}
