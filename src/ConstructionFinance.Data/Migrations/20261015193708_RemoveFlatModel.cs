using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstructionFinance.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFlatModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "finance_project_decisions");

            migrationBuilder.DropTable(
                name: "finance_project_lines");

            migrationBuilder.DropTable(
                name: "settlement_by_years");

            migrationBuilder.DropTable(
                name: "project_summaries");

            migrationBuilder.DropTable(
                name: "import_batches");

            migrationBuilder.DropColumn(
                name: "ContractAnnexValue",
                table: "finance_projects");

            migrationBuilder.DropColumn(
                name: "ContractValue",
                table: "finance_projects");

            migrationBuilder.DropColumn(
                name: "ContractorName",
                table: "finance_projects");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ContractAnnexValue",
                table: "finance_projects",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ContractValue",
                table: "finance_projects",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ContractorName",
                table: "finance_projects",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "finance_project_decisions",
                columns: table => new
                {
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    ApprovalDecisionId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_project_decisions", x => new { x.ProjectId, x.ApprovalDecisionId });
                    table.ForeignKey(
                        name: "FK_finance_project_decisions_approval_decisions_ApprovalDecisionId",
                        column: x => x.ApprovalDecisionId,
                        principalTable: "approval_decisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_finance_project_decisions_finance_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "finance_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "finance_project_lines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Advance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ContractRef = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DisbursedNSNN = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DisbursedNgoaiHoi = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PackageName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    PaymentKLHT = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    RecoveredAdvance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_project_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_finance_project_lines_finance_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "finance_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "import_batches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProjectsImported = table.Column<int>(type: "INTEGER", nullable: false),
                    SheetsFound = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceFileHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SourceFileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    WarningsJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_batches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "project_summaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImportBatchId = table.Column<int>(type: "INTEGER", nullable: false),
                    Advance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ContractValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ContractorName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DisbursedDTNH = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DisbursedNSNN = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PaymentKLHT = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PaymentToContractor = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ProjectName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    RecoveredAdvance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    SheetName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TotalDisbursed = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    UnrecoveredAdvance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_summaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_summaries_import_batches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "settlement_by_years",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectSummaryId = table.Column<int>(type: "INTEGER", nullable: false),
                    Advance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DTNH = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    KLHT = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    NSNN = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settlement_by_years", x => x.Id);
                    table.ForeignKey(
                        name: "FK_settlement_by_years_project_summaries_ProjectSummaryId",
                        column: x => x.ProjectSummaryId,
                        principalTable: "project_summaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_finance_project_decisions_ApprovalDecisionId",
                table: "finance_project_decisions",
                column: "ApprovalDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_project_lines_ProjectId_SortOrder",
                table: "finance_project_lines",
                columns: new[] { "ProjectId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_import_batches_ImportedAtUtc",
                table: "import_batches",
                column: "ImportedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_import_batches_SourceFileHash",
                table: "import_batches",
                column: "SourceFileHash");

            migrationBuilder.CreateIndex(
                name: "IX_project_summaries_ImportBatchId_SheetName",
                table: "project_summaries",
                columns: new[] { "ImportBatchId", "SheetName" });

            migrationBuilder.CreateIndex(
                name: "IX_settlement_by_years_ProjectSummaryId_Year",
                table: "settlement_by_years",
                columns: new[] { "ProjectSummaryId", "Year" });
        }
    }
}
