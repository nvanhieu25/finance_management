using ConstructionFinance.Application.Printing;

namespace ConstructionFinance.Desktop.Common;

/// Một màn hình khai báo "tôi in ra cái gì" bằng model thuần (PrintSheet). Màn hình không biết
/// gì về .xlsx — việc ghi tệp là của PrintWorkbookWriter, nên tầng Desktop không đụng tới thư viện
/// ghi Excel.
public interface IPrintablePage
{
    /// Null nghĩa là lúc này màn không in được — nút In mờ đi. Khác null PHẢI nghĩa là BuildPrintSheet()
    /// đang trả được một sheet thật: nút In bật theo thành viên này, nên một màn có tên mà không có sheet
    /// sẽ làm nút sáng lên rồi nhận về null. Điều kiện in được có thể đổi theo thời gian (vd. biểu mẫu
    /// GĐNTT chưa chọn gói thầu thì chưa in được), nên người cài đặt giữ hai thành viên đi cùng một điều kiện,
    /// và khi tên đổi mà không có lần đổi màn nào đi kèm thì phải bắn PropertyChanged(PrintSheetName). Bắn thừa
    /// (giá trị không đổi) thì được, vì người nghe chỉ đọc lại một giá trị rẻ; BỎ SÓT một lần đổi mới là lỗi, vì
    /// nút In sẽ mờ/sáng lệch cho tới lần đổi tab sau.
    string? PrintSheetName { get; }

    /// Nội dung sẽ in, đúng như đang hiện trên màn hình. Trả null khi PrintSheetName là null (màn đang
    /// không in được); khi PrintSheetName khác null thì phải trả một sheet.
    PrintSheet? BuildPrintSheet();
}

/// Một workbook gồm nhiều màn: "In toàn bộ" gom sheet của mọi màn in được vào một tệp.
public interface IPrintableWorkbook
{
    /// Tên cả workbook sẽ in, dùng đặt tên tệp: mã dự án đang chọn nếu có, không thì tên dự án.
    string PrintWorkbookName { get; }

    /// Có gì để "In toàn bộ" không: ít nhất một màn báo tên sheet in được. Nút In ở khung chung hỏi cái này mỗi
    /// lần cập nhật trạng thái bật/mờ, nên người cài đặt phải làm nó RẺ và KHÔNG có tác dụng phụ: chỉ đọc tên,
    /// không dựng sheet, không làm mới màn nào. Đừng dùng BuildAllSheets() để trả lời câu hỏi này.
    bool CanPrintAll { get; }

    /// ĐƯỜNG HÀNH ĐỘNG của "In toàn bộ", chạy một lần mỗi lần in thật. Sheet của mọi màn in được, theo thứ tự
    /// in; màn nào không có sheet thì bỏ qua, không chặn cả lô. Số liệu phải là mới nhất: người cài đặt tự làm
    /// mới các màn tổng hợp trước khi gom, người gọi không phải lo chuyện đó.
    IReadOnlyList<PrintSheet> BuildAllSheets();
}
