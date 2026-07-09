using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstructionFinance.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "finance_projects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ContractorName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    ContractValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_projects", x => x.Id);
                });

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
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PackageName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    ContractRef = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Advance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PaymentKLHT = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    RecoveredAdvance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DisbursedNSNN = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DisbursedNgoaiHoi = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_finance_project_decisions_ApprovalDecisionId",
                table: "finance_project_decisions",
                column: "ApprovalDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_project_lines_ProjectId_SortOrder",
                table: "finance_project_lines",
                columns: new[] { "ProjectId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "finance_project_decisions");

            migrationBuilder.DropTable(
                name: "finance_project_lines");

            migrationBuilder.DropTable(
                name: "finance_projects");
        }
    }
}
