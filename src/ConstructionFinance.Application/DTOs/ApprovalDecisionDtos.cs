namespace ConstructionFinance.Application.DTOs;

public record DecisionDto(
    int Id,
    string DecisionCode,
    string DecisionTitle,
    DateOnly DecisionDate,
    string IssuingAuthority,
    decimal TotalApprovedAmount,
    string? Notes);

public record CreateDecisionDto(
    string DecisionCode,
    string DecisionTitle,
    DateOnly DecisionDate,
    string IssuingAuthority,
    decimal TotalApprovedAmount,
    string? Notes);

public record UpdateDecisionDto(
    int Id,
    string DecisionCode,
    string DecisionTitle,
    DateOnly DecisionDate,
    string IssuingAuthority,
    decimal TotalApprovedAmount,
    string? Notes);
