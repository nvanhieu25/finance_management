using ConstructionFinance.Application.Common.Exceptions;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class ExceptionsTests
{
    [Fact]
    public void NotFoundException_message_includes_entity_and_id()
    {
        var ex = new NotFoundException("Vendor", 42);
        ex.Message.ShouldContain("Vendor");
        ex.Message.ShouldContain("42");
    }

    [Fact]
    public void BusinessRuleException_carries_message()
    {
        var ex = new BusinessRuleException("Không thể xóa khi còn hợp đồng con");
        ex.Message.ShouldBe("Không thể xóa khi còn hợp đồng con");
    }
}
