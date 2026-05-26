using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Application.DTOs;

public record ProjectDto(
    int Id,
    int ContractId,
    string ProjectCode,
    string ProjectName,
    string? ProjectType,
    string? Location,
    DateOnly? StartDate,
    DateOnly? EndDate,
    ProjectStatus Status,
    decimal TotalBudget,
    decimal TotalActual,
    string? Notes);

public record CreateProjectDto(
    int ContractId,
    string ProjectCode,
    string ProjectName,
    string? ProjectType,
    string? Location,
    DateOnly? StartDate,
    DateOnly? EndDate,
    decimal TotalBudget,
    string? Notes);

public record UpdateProjectDto(
    int Id,
    int ContractId,
    string ProjectCode,
    string ProjectName,
    string? ProjectType,
    string? Location,
    DateOnly? StartDate,
    DateOnly? EndDate,
    decimal TotalBudget,
    string? Notes);
