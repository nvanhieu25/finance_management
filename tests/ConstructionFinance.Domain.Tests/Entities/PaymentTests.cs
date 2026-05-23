using ConstructionFinance.Domain.Common;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Entities;

public class PaymentTests
{
    [Fact]
    public void Payment_extends_BaseEntity_not_SoftDeletable()
    {
        var payment = new Payment
        {
            CostItemId = 1,
            PaymentNo = 1,
            PaymentType = PaymentType.Advance,
            PaymentDate = new DateOnly(2026, 5, 1),
            Amount = 1_000_000m
        };

        payment.ShouldBeAssignableTo<BaseEntity>();
        payment.ShouldNotBeAssignableTo<SoftDeletableEntity>();
    }

    [Fact]
    public void Payment_has_required_fields()
    {
        var payment = new Payment
        {
            CostItemId = 5,
            PaymentNo = 2,
            PaymentType = PaymentType.Installment,
            PaymentDate = new DateOnly(2026, 5, 15),
            Amount = 500_000_000m,
            Percentage = 20m,
            InvoiceNo = "HD-001",
            InvoiceDate = new DateOnly(2026, 5, 14)
        };

        payment.CostItemId.ShouldBe(5);
        payment.PaymentType.ShouldBe(PaymentType.Installment);
        payment.Amount.ShouldBe(500_000_000m);
    }
}
