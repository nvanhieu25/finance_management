using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstructionFinance.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_settings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "approval_decisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DecisionCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DecisionTitle = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    DecisionDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    IssuingAuthority = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    TotalApprovedAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_decisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "attachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EntityId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    MimeType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EntityId = table.Column<int>(type: "INTEGER", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FieldName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cost_templates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TemplateName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cost_unit_types",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_unit_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vendors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VendorCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    VendorName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    TaxCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    ContactPerson = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vendors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cost_template_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentId = table.Column<int>(type: "INTEGER", nullable: true),
                    DefaultUnitId = table.Column<int>(type: "INTEGER", nullable: true),
                    ItemCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ItemName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_template_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cost_template_items_cost_template_items_ParentId",
                        column: x => x.ParentId,
                        principalTable: "cost_template_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cost_template_items_cost_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "cost_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cost_template_items_cost_unit_types_DefaultUnitId",
                        column: x => x.DefaultUnitId,
                        principalTable: "cost_unit_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "contracts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApprovalDecisionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ContractorVendorId = table.Column<int>(type: "INTEGER", nullable: true),
                    ContractCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ContractName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ContractDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    SigningDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ContractValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contracts_approval_decisions_ApprovalDecisionId",
                        column: x => x.ApprovalDecisionId,
                        principalTable: "approval_decisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contracts_vendors_ContractorVendorId",
                        column: x => x.ContractorVendorId,
                        principalTable: "vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContractId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ProjectName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ProjectType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TotalBudget = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalActual = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_projects_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cost_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentId = table.Column<int>(type: "INTEGER", nullable: true),
                    UnitTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    VendorId = table.Column<int>(type: "INTEGER", nullable: true),
                    ItemCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ItemName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsLeaf = table.Column<bool>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    EstimatedAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    BidPackage = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cost_items_cost_items_ParentId",
                        column: x => x.ParentId,
                        principalTable: "cost_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cost_items_cost_unit_types_UnitTypeId",
                        column: x => x.UnitTypeId,
                        principalTable: "cost_unit_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_cost_items_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cost_items_vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CostItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentNo = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Percentage = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    InvoiceNo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    InvoiceDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payments_cost_items_CostItemId",
                        column: x => x.CostItemId,
                        principalTable: "cost_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_approval_decisions_DecisionCode",
                table: "approval_decisions",
                column: "DecisionCode",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_attachments_EntityType_EntityId",
                table: "attachments",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_EntityType_EntityId_ChangedAt",
                table: "audit_logs",
                columns: new[] { "EntityType", "EntityId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_contracts_ApprovalDecisionId",
                table: "contracts",
                column: "ApprovalDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_contracts_ApprovalDecisionId_ContractCode",
                table: "contracts",
                columns: new[] { "ApprovalDecisionId", "ContractCode" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_contracts_ContractorVendorId",
                table: "contracts",
                column: "ContractorVendorId");

            migrationBuilder.CreateIndex(
                name: "IX_cost_items_ParentId",
                table: "cost_items",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_cost_items_Path",
                table: "cost_items",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "IX_cost_items_ProjectId",
                table: "cost_items",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_cost_items_ProjectId_ParentId_SortOrder",
                table: "cost_items",
                columns: new[] { "ProjectId", "ParentId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_cost_items_UnitTypeId",
                table: "cost_items",
                column: "UnitTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_cost_items_VendorId",
                table: "cost_items",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_cost_template_items_DefaultUnitId",
                table: "cost_template_items",
                column: "DefaultUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_cost_template_items_ParentId",
                table: "cost_template_items",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_cost_template_items_TemplateId_ParentId_SortOrder",
                table: "cost_template_items",
                columns: new[] { "TemplateId", "ParentId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_cost_templates_TemplateName",
                table: "cost_templates",
                column: "TemplateName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cost_unit_types_Code",
                table: "cost_unit_types",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_CostItemId_PaymentDate",
                table: "payments",
                columns: new[] { "CostItemId", "PaymentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_payments_CostItemId_PaymentNo",
                table: "payments",
                columns: new[] { "CostItemId", "PaymentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_projects_ContractId",
                table: "projects",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_ContractId_ProjectCode",
                table: "projects",
                columns: new[] { "ContractId", "ProjectCode" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_vendors_TaxCode",
                table: "vendors",
                column: "TaxCode",
                unique: true,
                filter: "\"TaxCode\" IS NOT NULL AND \"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_settings");

            migrationBuilder.DropTable(
                name: "attachments");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "cost_template_items");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "cost_templates");

            migrationBuilder.DropTable(
                name: "cost_items");

            migrationBuilder.DropTable(
                name: "cost_unit_types");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "contracts");

            migrationBuilder.DropTable(
                name: "approval_decisions");

            migrationBuilder.DropTable(
                name: "vendors");
        }
    }
}
