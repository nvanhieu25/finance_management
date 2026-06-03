using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstructionFinance.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "import_batches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceFileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    SourceFileHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SheetsFound = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectsImported = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    WarningsJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                    SheetName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ProjectName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ContractorName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ContractValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Advance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PaymentKLHT = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    RecoveredAdvance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PaymentToContractor = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    UnrecoveredAdvance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DisbursedNSNN = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DisbursedDTNH = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalDisbursed = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
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
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    KLHT = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    Advance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    NSNN = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    DTNH = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                name: "IX_project_summaries_ImportBatchId_SheetName",
                table: "project_summaries",
                columns: new[] { "ImportBatchId", "SheetName" });

            migrationBuilder.CreateIndex(
                name: "IX_settlement_by_years_ProjectSummaryId_Year",
                table: "settlement_by_years",
                columns: new[] { "ProjectSummaryId", "Year" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "settlement_by_years");

            migrationBuilder.DropTable(
                name: "project_summaries");

            migrationBuilder.DropTable(
                name: "import_batches");
        }
    }
}
