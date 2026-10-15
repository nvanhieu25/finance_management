using ConstructionFinance.Application.Printing;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Forms;

/// Mọi thứ một lần dựng biểu mẫu cần: các đợt đã trải phẳng, đợt nào đang chọn, chữ người dùng đã gõ,
/// tên đơn vị, tên dự án và năm (tham số để test không phụ thuộc đồng hồ).
public sealed record FormBuildContext(
    IReadOnlyList<FormDotRow> Rows,
    IReadOnlySet<FormDotKey> Selected,
    FormInputSet Inputs,
    FormOrgInfo Org,
    string ProjectName,
    int Year);

/// `Sheet` LUÔN có (kể cả khi `Error` khác null — tờ trống để người dùng vẫn gõ được các ô chữ).
/// `Error` khác null nghĩa là chưa xuất/in được; `PrintBlockReason` là cụm viết thường không dấu chấm
/// cho thông báo "In toàn bộ".
public sealed record FormBuildResult(ResolvedSheet Sheet, string? Error, string? PrintBlockReason = null)
{
    public bool CanOutput => Error is null;
}

/// Một biểu mẫu: bản gốc + cách dựng số từ các builder có sẵn. Không có cách tính số mới nào ở đây.
public abstract class FormDefinition
{
    protected FormDefinition(FormTemplate template) => Template = template;

    /// Khoá lưu trong `finance_form_inputs.Form`. ĐỔI là mất chữ đã gõ — đừng đổi.
    public abstract string Id { get; }
    public abstract string Title { get; }
    public abstract string ExportPrefix { get; }
    public abstract string ExportDialogTitle { get; }
    public abstract Fund? FundFilter { get; }
    public abstract PrintPageProfile PrintProfile { get; }

    public FormTemplate Template { get; }

    public abstract FormBuildResult Build(FormBuildContext context);
}

public sealed class ThongTriDefinition : FormDefinition
{
    public ThongTriDefinition() : base(ThongTriTemplate.Create()) { }

    public override string Id => "thongtri";
    public override string Title => "Thông tri";
    public override string ExportPrefix => "ThongTri";
    public override string ExportDialogTitle => "Xuất Thông tri";
    public override Fund? FundFilter => Fund.NSNN;
    public override PrintPageProfile PrintProfile => PrintPageProfiles.ThongTri;

    public override FormBuildResult Build(FormBuildContext context)
    {
        var form = ThongTriBuilder.Build(context.Rows, context.Selected, null, context.Org, context.ProjectName);
        var values = new FormValues()
            .Number("Total", form.Total)
            .Text("TotalInWords", form.TotalInWords);

        return new FormBuildResult(
            FormSheetResolver.Resolve(Template, values, context.Inputs, context.Org, context.Year, form.Lines),
            Error: null);
    }
}

public sealed class KsChiDefinition : FormDefinition
{
    public KsChiDefinition() : base(KsChiTemplate.Create()) { }

    public override string Id => "kschi";
    public override string Title => "KS chi";
    public override string ExportPrefix => "KSChi";
    public override string ExportDialogTitle => "Xuất phần ghi của KS chi";
    public override Fund? FundFilter => null;
    public override PrintPageProfile PrintProfile => PrintPageProfiles.KsChi;

    public override FormBuildResult Build(FormBuildContext context)
    {
        // Số tự nhập (thu hồi các năm, thuế GTGT, số từ chối) không đi vào phép tính: Mẫu để "Số trả đơn vị
        // thụ hưởng" = chấp nhận − thu hồi tạm ứng, nên builder không cần bản khai.
        var form = KsChiBuilder.Build(context.Rows, context.Selected, null);
        var values = new FormValues()
            .Number("Accepted.Total", form.Accepted.Total)
            .Number("Accepted.NSNN", form.Accepted.NSNN)
            .Number("Accepted.NgoaiHoi", form.Accepted.NgoaiHoi)
            .Number("Recovered.Total", form.Recovered.Total)
            .Number("Recovered.NSNN", form.Recovered.NSNN)
            .Number("Recovered.NgoaiHoi", form.Recovered.NgoaiHoi)
            .Number("Paid.Total", form.PaidToBeneficiary.Total)
            .Number("Paid.NSNN", form.PaidToBeneficiary.NSNN)
            .Number("Paid.NgoaiHoi", form.PaidToBeneficiary.NgoaiHoi)
            .Text("PaidInWords", form.PaidInWords)
            .Text("GhiChu", form.GhiChu);

        return new FormBuildResult(
            FormSheetResolver.Resolve(Template, values, context.Inputs, context.Org, context.Year),
            Error: null);
    }
}

public sealed class GdnttDefinition : FormDefinition
{
    public GdnttDefinition() : base(GdnttTemplate.Create()) { }

    public override string Id => "gdntt";
    public override string Title => "GĐNTT";
    public override string ExportPrefix => "GDNTT";
    public override string ExportDialogTitle => "Xuất Giấy đề nghị thanh toán";
    public override Fund? FundFilter => Fund.NgoaiHoi;
    public override PrintPageProfile PrintProfile => PrintPageProfiles.Gdntt;

    public override FormBuildResult Build(FormBuildContext context)
    {
        // Hai ô số tự nhập duy nhất mà builder dùng (Số trả nhà thầu = đề nghị − thu hồi − bảo hành − tạm giữ).
        var declaration = new GdnttDeclaration
        {
            ChuyenTienBaoHanh = context.Inputs.GetNumber("ChuyenTienBaoHanh"),
            ChuyenTienTamGiu = context.Inputs.GetNumber("ChuyenTienTamGiu"),
        };
        var result = GdnttBuilder.Build(context.Rows, context.Selected, declaration, context.Org);

        if (result.Form is null)
        {
            // Tờ trống nhưng vẫn hiện: tên dự án điền sẵn, các ô chữ vẫn gõ được; chỉ khoá xuất và in.
            var blank = new FormValues { BlankMissing = true }.Text("TenDuAnCongTrinh", context.ProjectName);
            var anySelected = context.Rows.Any(row =>
                context.Selected.Contains(row.Key) && row.Fund == Fund.NgoaiHoi);
            return new FormBuildResult(
                FormSheetResolver.Resolve(Template, blank, context.Inputs, context.Org, context.Year),
                result.Error,
                // Đúng với việc người dùng đã làm: chưa tích đợt nào ≠ đã tích đợt của nhiều gói.
                anySelected ? "đã chọn đợt của nhiều gói thầu" : "chưa chọn gói thầu");
        }

        var form = result.Form;
        var values = new FormValues()
            .Text("TenDuAnCongTrinh", context.ProjectName)
            .Number("UnrecoveredAdvanceBefore", form.UnrecoveredAdvanceBefore)
            .Text("PackageName", form.PackageName)
            .Number("ContractTotal", form.ContractTotal)
            .Number("CumulativeBefore", form.CumulativeBefore)
            .Number("Requested", form.RequestedThisPeriod)
            .Text("RequestedInWords", form.RequestedInWords)
            .Number("Recovered", form.RecoveredThisPeriod)
            .Number("PaidToContractor", form.PaidToContractor)
            .Text("ContractorName", form.ContractorName)
            .Text("BankAccount", form.BankAccount);

        return new FormBuildResult(
            FormSheetResolver.Resolve(Template, values, context.Inputs, context.Org, context.Year),
            Error: null);
    }
}

public static class FormDefinitions
{
    public static ThongTriDefinition ThongTri { get; } = new();
    public static KsChiDefinition KsChi { get; } = new();
    public static GdnttDefinition Gdntt { get; } = new();

    /// Thứ tự là thứ tự ba tab.
    public static IReadOnlyList<FormDefinition> All { get; } = new FormDefinition[] { ThongTri, KsChi, Gdntt };
}
