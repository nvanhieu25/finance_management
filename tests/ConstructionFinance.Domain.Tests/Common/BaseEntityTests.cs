using ConstructionFinance.Domain.Common;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Common;

public class BaseEntityTests
{
    private class TestEntity : BaseEntity { }
    private class TestSoftDeletableEntity : SoftDeletableEntity { }

    [Fact]
    public void BaseEntity_should_have_Id_CreatedAt_UpdatedAt_with_defaults()
    {
        var entity = new TestEntity();

        entity.Id.ShouldBe(0);
        entity.CreatedAt.ShouldBe(default);
        entity.UpdatedAt.ShouldBe(default);
    }

    [Fact]
    public void SoftDeletableEntity_should_extend_BaseEntity_with_DeletedAt()
    {
        var entity = new TestSoftDeletableEntity();

        entity.Id.ShouldBe(0);
        entity.DeletedAt.ShouldBeNull();
        entity.ShouldBeAssignableTo<BaseEntity>();
        entity.ShouldBeAssignableTo<ISoftDeletable>();
    }

    [Fact]
    public void ISoftDeletable_allows_setting_DeletedAt()
    {
        ISoftDeletable entity = new TestSoftDeletableEntity();
        var now = DateTime.UtcNow;

        entity.DeletedAt = now;

        entity.DeletedAt.ShouldBe(now);
    }
}
