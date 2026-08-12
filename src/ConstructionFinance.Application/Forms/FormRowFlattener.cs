using ConstructionFinance.Application.Calc;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Forms;

/// Trải cây Vị trí → nhóm chi phí → gói thầu → đợt thành một danh sách phẳng theo
/// đúng thứ tự người dùng thấy trong sổ. Khóa của mỗi dòng là vị trí của nó trong
/// cây, nên Desktop quy được lựa chọn (giữ theo tham chiếu dòng lưới) về khóa.
public static class FormRowFlattener
{
    public static IReadOnlyList<FormDotRow> Flatten(IReadOnlyList<ViTri> viTris)
    {
        var rows = new List<FormDotRow>();
        var orderedViTris = viTris.OrderBy(v => v.SortOrder).ToList();

        for (var vi = 0; vi < orderedViTris.Count; vi++)
        {
            var groups = orderedViTris[vi].CostGroups.OrderBy(g => g.SortOrder).ToList();
            for (var gi = 0; gi < groups.Count; gi++)
            {
                var packages = groups[gi].Packages.OrderBy(p => p.SortOrder).ToList();
                for (var pi = 0; pi < packages.Count; pi++)
                {
                    var package = packages[pi];
                    var dots = package.Dots.OrderBy(d => d.SortOrder).ToList();
                    for (var di = 0; di < dots.Count; di++)
                    {
                        var dot = dots[di];
                        rows.Add(new FormDotRow(
                            Key: new FormDotKey(vi, gi, pi, di),
                            ViTriName: orderedViTris[vi].Name,
                            CostGroupName: groups[gi].Name,
                            PackageName: package.Name,
                            ContractorName: package.ContractorName,
                            BankAccount: package.BankAccount,
                            ContractValue: package.ContractValue,
                            ContractAnnexValue: package.ContractAnnexValue,
                            Date: dot.Date,
                            Content: dot.Content,
                            DocumentNo: dot.DocumentNo,
                            Fund: dot.Fund,
                            Advance: dot.Advance,
                            PaymentKLHT: dot.PaymentKLHT,
                            RecoveredAdvance: dot.RecoveredAdvance,
                            // Một nguồn công thức duy nhất: dùng lại đúng hàm của sổ.
                            Disbursed: TreeCalculator.Disbursed(dot.ToDotInput())));
                    }
                }
            }
        }

        return rows;
    }
}
