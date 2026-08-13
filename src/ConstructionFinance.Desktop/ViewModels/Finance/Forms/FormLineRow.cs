namespace ConstructionFinance.Desktop.ViewModels.Finance.Forms;

/// Một dòng bảng trong bản xem trước biểu mẫu. Toàn chuỗi — View không tự định dạng.
public class FormLineRow
{
    public int Indent { get; init; }
    public string Content { get; init; } = "";
    public string AmountText { get; init; } = "";
    public bool IsBold { get; init; }
}
