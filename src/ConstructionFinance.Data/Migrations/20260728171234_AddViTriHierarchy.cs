using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstructionFinance.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddViTriHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "finance_vitris",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    NguonVon = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_vitris", x => x.Id);
                    table.ForeignKey(
                        name: "FK_finance_vitris_finance_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "finance_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "finance_cost_groups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ViTriId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_cost_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_finance_cost_groups_finance_vitris_ViTriId",
                        column: x => x.ViTriId,
                        principalTable: "finance_vitris",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "finance_packages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CostGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ContractorName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    BankAccount = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    ContractValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ContractAnnexValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_packages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_finance_packages_finance_cost_groups_CostGroupId",
                        column: x => x.CostGroupId,
                        principalTable: "finance_cost_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "finance_dots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PackageId = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    DocumentNo = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Fund = table.Column<int>(type: "INTEGER", nullable: false),
                    Advance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PaymentKLHT = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    RecoveredAdvance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_dots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_finance_dots_finance_packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "finance_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_finance_cost_groups_ViTriId_SortOrder",
                table: "finance_cost_groups",
                columns: new[] { "ViTriId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_dots_PackageId_SortOrder",
                table: "finance_dots",
                columns: new[] { "PackageId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_packages_CostGroupId_SortOrder",
                table: "finance_packages",
                columns: new[] { "CostGroupId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_vitris_ProjectId_SortOrder",
                table: "finance_vitris",
                columns: new[] { "ProjectId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "finance_dots");

            migrationBuilder.DropTable(
                name: "finance_packages");

            migrationBuilder.DropTable(
                name: "finance_cost_groups");

            migrationBuilder.DropTable(
                name: "finance_vitris");
        }
    }
}
