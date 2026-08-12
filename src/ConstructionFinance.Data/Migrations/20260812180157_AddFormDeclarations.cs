using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstructionFinance.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFormDeclarations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "finance_gdntt_decl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    So = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    NgayThangNam = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    KinhGui = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    TenDuAnCongTrinh = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    ChuDauTu = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    CanCuQuyetDinh = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    CanCuHopDong = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    CanCuGiayDeNghi = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    ThuocKeHoach = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    TaiNganHang = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    ChuyenTienBaoHanh = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ChuyenTienTamGiu = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_gdntt_decl", x => x.Id);
                    table.ForeignKey(
                        name: "FK_finance_gdntt_decl_finance_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "finance_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "finance_kschi_decl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    NgayNhanGiayDeNghi = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    MucTieuMuc1 = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    MucTieuMuc2 = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    MucTieuMuc3 = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    MucTieuMuc4 = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    ThuHoiCacNamTruoc = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ThuHoiNamNay = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ThueGTGT = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    SoTuChoi = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    LyDoTuChoi = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    GhiChu = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_kschi_decl", x => x.Id);
                    table.ForeignKey(
                        name: "FK_finance_kschi_decl_finance_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "finance_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "finance_thongtri_decl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    SoThongTri = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    NgayGhiSo = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    TenCTMT = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    MaSo = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    VeThangNam = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    TaiKhoan1 = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    DonVi1 = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    SoChungTu1 = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    TaiKhoan2 = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    DonVi2 = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    SoChungTu2 = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    LoaiNganSach = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Khoan = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Muc = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    TieuMuc = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    TietMuc = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Nganh = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_thongtri_decl", x => x.Id);
                    table.ForeignKey(
                        name: "FK_finance_thongtri_decl_finance_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "finance_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_finance_gdntt_decl_ProjectId",
                table: "finance_gdntt_decl",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_kschi_decl_ProjectId",
                table: "finance_kschi_decl",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_thongtri_decl_ProjectId",
                table: "finance_thongtri_decl",
                column: "ProjectId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "finance_gdntt_decl");

            migrationBuilder.DropTable(
                name: "finance_kschi_decl");

            migrationBuilder.DropTable(
                name: "finance_thongtri_decl");
        }
    }
}
