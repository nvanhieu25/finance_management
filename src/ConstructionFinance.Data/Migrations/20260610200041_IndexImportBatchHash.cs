using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstructionFinance.Data.Migrations
{
    /// <inheritdoc />
    public partial class IndexImportBatchHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_import_batches_ImportedAtUtc",
                table: "import_batches",
                column: "ImportedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_import_batches_SourceFileHash",
                table: "import_batches",
                column: "SourceFileHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_import_batches_ImportedAtUtc",
                table: "import_batches");

            migrationBuilder.DropIndex(
                name: "IX_import_batches_SourceFileHash",
                table: "import_batches");
        }
    }
}
