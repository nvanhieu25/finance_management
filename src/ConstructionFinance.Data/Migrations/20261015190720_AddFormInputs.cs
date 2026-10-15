using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstructionFinance.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFormInputs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "finance_form_inputs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Form = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_form_inputs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_finance_form_inputs_finance_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "finance_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_finance_form_inputs_ProjectId_Form_Key",
                table: "finance_form_inputs",
                columns: new[] { "ProjectId", "Form", "Key" },
                unique: true);

            // Chép chữ người dùng đã gõ từ ba bảng cũ sang bảng khoá-giá trị. Chỉ chép giá trị khác NULL và khác rỗng
            // (rỗng ở bảng cũ nghĩa là "chưa gõ", không phải "đã xoá"). Số (decimal TEXT) chép nguyên chuỗi — đã ở dạng
            // bất biến văn hoá. KS chi: bản cũ lưu MỘT chuỗi "mục, tiểu mục" mỗi dòng → vào khoá Muc{i} (bản mới tách
            // hai ô; chuỗi cũ nằm trọn ở ô Mục, người dùng tự tách khi mở lại).
            migrationBuilder.Sql(@"
INSERT INTO finance_form_inputs (ProjectId, Form, Key, Value, CreatedAt, UpdatedAt)
SELECT ProjectId, 'thongtri', 'SoThongTri', SoThongTri, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE SoThongTri IS NOT NULL AND SoThongTri <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'NgayGhiSo', NgayGhiSo, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE NgayGhiSo IS NOT NULL AND NgayGhiSo <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'TenCTMT', TenCTMT, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE TenCTMT IS NOT NULL AND TenCTMT <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'MaSo', MaSo, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE MaSo IS NOT NULL AND MaSo <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'VeThangNam', VeThangNam, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE VeThangNam IS NOT NULL AND VeThangNam <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'TaiKhoan1', TaiKhoan1, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE TaiKhoan1 IS NOT NULL AND TaiKhoan1 <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'DonVi1', DonVi1, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE DonVi1 IS NOT NULL AND DonVi1 <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'SoChungTu1', SoChungTu1, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE SoChungTu1 IS NOT NULL AND SoChungTu1 <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'TaiKhoan2', TaiKhoan2, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE TaiKhoan2 IS NOT NULL AND TaiKhoan2 <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'DonVi2', DonVi2, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE DonVi2 IS NOT NULL AND DonVi2 <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'SoChungTu2', SoChungTu2, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE SoChungTu2 IS NOT NULL AND SoChungTu2 <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'LoaiNganSach', LoaiNganSach, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE LoaiNganSach IS NOT NULL AND LoaiNganSach <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'Khoan', Khoan, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE Khoan IS NOT NULL AND Khoan <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'Muc', Muc, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE Muc IS NOT NULL AND Muc <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'TieuMuc', TieuMuc, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE TieuMuc IS NOT NULL AND TieuMuc <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'TietMuc', TietMuc, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE TietMuc IS NOT NULL AND TietMuc <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'Nganh', Nganh, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE Nganh IS NOT NULL AND Nganh <> ''
UNION ALL SELECT ProjectId, 'kschi', 'NgayNhanGiayDeNghi', NgayNhanGiayDeNghi, CreatedAt, UpdatedAt FROM finance_kschi_decl WHERE NgayNhanGiayDeNghi IS NOT NULL AND NgayNhanGiayDeNghi <> ''
UNION ALL SELECT ProjectId, 'kschi', 'Muc1', MucTieuMuc1, CreatedAt, UpdatedAt FROM finance_kschi_decl WHERE MucTieuMuc1 IS NOT NULL AND MucTieuMuc1 <> ''
UNION ALL SELECT ProjectId, 'kschi', 'Muc2', MucTieuMuc2, CreatedAt, UpdatedAt FROM finance_kschi_decl WHERE MucTieuMuc2 IS NOT NULL AND MucTieuMuc2 <> ''
UNION ALL SELECT ProjectId, 'kschi', 'Muc3', MucTieuMuc3, CreatedAt, UpdatedAt FROM finance_kschi_decl WHERE MucTieuMuc3 IS NOT NULL AND MucTieuMuc3 <> ''
UNION ALL SELECT ProjectId, 'kschi', 'Muc4', MucTieuMuc4, CreatedAt, UpdatedAt FROM finance_kschi_decl WHERE MucTieuMuc4 IS NOT NULL AND MucTieuMuc4 <> ''
UNION ALL SELECT ProjectId, 'kschi', 'ThuHoiCacNamTruoc', ThuHoiCacNamTruoc, CreatedAt, UpdatedAt FROM finance_kschi_decl WHERE CAST(ThuHoiCacNamTruoc AS REAL) <> 0
UNION ALL SELECT ProjectId, 'kschi', 'ThuHoiNamNay', ThuHoiNamNay, CreatedAt, UpdatedAt FROM finance_kschi_decl WHERE CAST(ThuHoiNamNay AS REAL) <> 0
UNION ALL SELECT ProjectId, 'kschi', 'ThueGTGT', ThueGTGT, CreatedAt, UpdatedAt FROM finance_kschi_decl WHERE CAST(ThueGTGT AS REAL) <> 0
UNION ALL SELECT ProjectId, 'kschi', 'SoTuChoi', SoTuChoi, CreatedAt, UpdatedAt FROM finance_kschi_decl WHERE CAST(SoTuChoi AS REAL) <> 0
UNION ALL SELECT ProjectId, 'kschi', 'LyDoTuChoi', LyDoTuChoi, CreatedAt, UpdatedAt FROM finance_kschi_decl WHERE LyDoTuChoi IS NOT NULL AND LyDoTuChoi <> ''
UNION ALL SELECT ProjectId, 'kschi', 'GhiChu', GhiChu, CreatedAt, UpdatedAt FROM finance_kschi_decl WHERE GhiChu IS NOT NULL AND GhiChu <> ''
UNION ALL SELECT ProjectId, 'gdntt', 'So', So, CreatedAt, UpdatedAt FROM finance_gdntt_decl WHERE So IS NOT NULL AND So <> ''
UNION ALL SELECT ProjectId, 'gdntt', 'NgayThangNam', 'Hải phòng, ngày ' || NgayThangNam, CreatedAt, UpdatedAt FROM finance_gdntt_decl WHERE NgayThangNam IS NOT NULL AND NgayThangNam <> ''
UNION ALL SELECT ProjectId, 'gdntt', 'KinhGui', KinhGui, CreatedAt, UpdatedAt FROM finance_gdntt_decl WHERE KinhGui IS NOT NULL AND KinhGui <> ''
UNION ALL SELECT ProjectId, 'gdntt', 'TenDuAnCongTrinh', TenDuAnCongTrinh, CreatedAt, UpdatedAt FROM finance_gdntt_decl WHERE TenDuAnCongTrinh IS NOT NULL AND TenDuAnCongTrinh <> ''
UNION ALL SELECT ProjectId, 'gdntt', 'ChuDauTu', ChuDauTu, CreatedAt, UpdatedAt FROM finance_gdntt_decl WHERE ChuDauTu IS NOT NULL AND ChuDauTu <> ''
UNION ALL SELECT ProjectId, 'gdntt', 'CanCuQuyetDinh', CanCuQuyetDinh, CreatedAt, UpdatedAt FROM finance_gdntt_decl WHERE CanCuQuyetDinh IS NOT NULL AND CanCuQuyetDinh <> ''
UNION ALL SELECT ProjectId, 'gdntt', 'CanCuHopDong', CanCuHopDong, CreatedAt, UpdatedAt FROM finance_gdntt_decl WHERE CanCuHopDong IS NOT NULL AND CanCuHopDong <> ''
UNION ALL SELECT ProjectId, 'gdntt', 'CanCuGiayDeNghi', CanCuGiayDeNghi, CreatedAt, UpdatedAt FROM finance_gdntt_decl WHERE CanCuGiayDeNghi IS NOT NULL AND CanCuGiayDeNghi <> ''
UNION ALL SELECT ProjectId, 'gdntt', 'ThuocKeHoach', ThuocKeHoach, CreatedAt, UpdatedAt FROM finance_gdntt_decl WHERE ThuocKeHoach IS NOT NULL AND ThuocKeHoach <> ''
UNION ALL SELECT ProjectId, 'gdntt', 'TaiNganHang', TaiNganHang, CreatedAt, UpdatedAt FROM finance_gdntt_decl WHERE TaiNganHang IS NOT NULL AND TaiNganHang <> ''
UNION ALL SELECT ProjectId, 'gdntt', 'ChuyenTienBaoHanh', ChuyenTienBaoHanh, CreatedAt, UpdatedAt FROM finance_gdntt_decl WHERE CAST(ChuyenTienBaoHanh AS REAL) <> 0
UNION ALL SELECT ProjectId, 'gdntt', 'ChuyenTienTamGiu', ChuyenTienTamGiu, CreatedAt, UpdatedAt FROM finance_gdntt_decl WHERE CAST(ChuyenTienTamGiu AS REAL) <> 0;
");

            migrationBuilder.DropTable(
                name: "finance_gdntt_decl");

            migrationBuilder.DropTable(
                name: "finance_kschi_decl");

            migrationBuilder.DropTable(
                name: "finance_thongtri_decl");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dữ liệu không được chép ngược: ba bảng cũ được tạo lại RỖNG.
            migrationBuilder.DropTable(
                name: "finance_form_inputs");

            migrationBuilder.CreateTable(
                name: "finance_gdntt_decl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    CanCuGiayDeNghi = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    CanCuHopDong = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    CanCuQuyetDinh = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    ChuDauTu = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    ChuyenTienBaoHanh = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ChuyenTienTamGiu = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    KinhGui = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    NgayThangNam = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    So = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    TaiNganHang = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    TenDuAnCongTrinh = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    ThuocKeHoach = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
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
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GhiChu = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    LyDoTuChoi = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    MucTieuMuc1 = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    MucTieuMuc2 = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    MucTieuMuc3 = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    MucTieuMuc4 = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    NgayNhanGiayDeNghi = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    SoTuChoi = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ThuHoiCacNamTruoc = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ThuHoiNamNay = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ThueGTGT = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
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
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DonVi1 = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    DonVi2 = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Khoan = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    LoaiNganSach = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    MaSo = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Muc = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Nganh = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    NgayGhiSo = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    SoChungTu1 = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    SoChungTu2 = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    SoThongTri = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    TaiKhoan1 = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    TaiKhoan2 = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    TenCTMT = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    TietMuc = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    TieuMuc = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    VeThangNam = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true)
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
    }
}
