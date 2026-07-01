using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Validators;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Validators
        services.AddScoped<IValidator<Vendor>, VendorValidator>();
        services.AddScoped<IValidator<ApprovalDecision>, ApprovalDecisionValidator>();
        services.AddScoped<IValidator<Contract>, ContractValidator>();
        services.AddScoped<IValidator<Project>, ProjectValidator>();
        services.AddScoped<IValidator<CostItem>, CostItemValidator>();
        services.AddScoped<IValidator<Payment>, PaymentValidator>();

        // Services
        services.AddScoped<IVendorService, VendorService>();
        services.AddScoped<IDecisionService, DecisionService>();
        services.AddScoped<IContractService, ContractService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ICostService, CostService>();
        services.AddScoped<ICostUnitTypeService, CostUnitTypeService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAppSettingService, AppSettingService>();
        services.AddScoped<ICompanyInfoService, CompanyInfoService>();
        services.AddScoped<IExcelImportService, ExcelImportService>();
        services.AddScoped<IExcelExportService, ExcelExportService>();
        services.AddScoped<IProjectSummaryService, ProjectSummaryService>();
        services.AddScoped<IFinanceProjectService, FinanceProjectService>();
        services.AddScoped<IFinanceImportService, FinanceImportService>();
        services.AddScoped<IFinanceExportService, FinanceExportService>();

        return services;
    }
}
