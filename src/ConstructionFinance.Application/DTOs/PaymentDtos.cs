using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Application.DTOs;

public record PaymentDto(
    int Id,
    int CostItemId,
    int PaymentNo,
    PaymentType PaymentType,
    DateOnly PaymentDate,
    decimal Amount,
    decimal Percentage,
    string? InvoiceNo,
    DateOnly? InvoiceDate,
    string? Description,
    string? Notes);

// Percentage nullable: if null, service auto-computes from cost item estimated amount.
public record CreatePaymentDto(
    int CostItemId,
    PaymentType PaymentType,
    DateOnly PaymentDate,
    decimal Amount,
    decimal? Percentage,
    string? InvoiceNo,
    DateOnly? InvoiceDate,
    string? Description,
    string? Notes);

public record UpdatePaymentDto(
    int Id,
    PaymentType PaymentType,
    DateOnly PaymentDate,
    decimal Amount,
    decimal? Percentage,
    string? InvoiceNo,
    DateOnly? InvoiceDate,
    string? Description,
    string? Notes);

public record PaymentSummaryDto(
    int CostItemId,
    decimal EstimatedAmount,
    decimal TotalPaid,
    decimal Remaining,
    decimal CompletionPct);
