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

    /// Khi PrintSheetName là null: VÌ SAO màn này chưa in được, bằng một cụm từ tiếng Việt viết thường, không dấu chấm cuối
    /// (vd. "chưa chọn gói thầu"), để "In toàn bộ" nói được với người dùng sheet nào đã bị bỏ và vì sao (spec §8). Mặc định null:
    /// màn không có gì để giải thích. Chỉ để ĐỌC lúc báo; in được hay không vẫn do PrintSheetName quyết định. Lý do phải đúng với
    /// việc người dùng đã làm: màn có nhiều lý do không in được thì trả đúng lý do hiện tại, không trả một câu dùng chung.
    string? PrintUnavailableReason => null;
}

/// Một trang mà "In toàn bộ" đã bỏ qua vì không dựng được sheet. Title là chữ trên tab, tức thứ người dùng nhìn thấy (trang không
/// in được thì PrintSheetName là null nên không gọi tên bằng nó); Reason là lý do, null nếu trang không nêu.
public sealed record SkippedPrintPage(string Title, string? Reason);

/// Kết quả gom của "In toàn bộ": các sheet dựng được, theo thứ tự in, kèm các trang bị bỏ qua, để người gọi nói với người dùng
/// bộ tệp này thiếu gì thay vì im lặng giao một bộ thiếu.
public sealed record PrintAllResult(IReadOnlyList<PrintSheet> Sheets, IReadOnlyList<SkippedPrintPage> Skipped);

/// Một workbook gồm nhiều màn: "In toàn bộ" gom sheet của mọi màn in được vào một tệp.
public interface IPrintableWorkbook
{
    /// Tên cả workbook sẽ in, dùng đặt tên tệp: mã dự án đang chọn nếu có, không thì tên dự án.
    string PrintWorkbookName { get; }

    /// Tên dự án in ở chân trang (bên trái): TÊN dự án, KHÔNG phải mã. Chân trang là thứ người nhận bản in đọc, cấp trên hoặc
    /// kiểm toán không biết mã nội bộ (spec §5: "tên dự án · tên vị trí"). Hai thành viên cố ý tách đôi vì hai người đọc khác nhau:
    /// mã đặt tên tệp (PrintWorkbookName), tên đứng trên giấy (thành viên này).
    string PrintFooterProjectName { get; }

    /// Có gì để "In toàn bộ" không: ít nhất một màn báo tên sheet in được. Nút In ở khung chung hỏi cái này mỗi
    /// lần cập nhật trạng thái bật/mờ, nên người cài đặt phải làm nó RẺ và KHÔNG có tác dụng phụ: chỉ đọc tên,
    /// không dựng sheet, không làm mới màn nào. Đừng dùng BuildAllSheets() để trả lời câu hỏi này.
    bool CanPrintAll { get; }

    /// ĐƯỜNG HÀNH ĐỘNG của "In toàn bộ", chạy một lần mỗi lần in thật. Sheet của mọi màn in được, theo thứ tự
    /// in; màn nào không có sheet thì bỏ qua, không chặn cả lô, nhưng được LIỆT KÊ trong Skipped (kèm lý do nếu có) để người
    /// gọi báo cho người dùng. Số liệu phải là mới nhất: người cài đặt tự làm mới các màn tổng hợp trước khi gom, người gọi
    /// không phải lo chuyện đó.
    PrintAllResult BuildAllSheets();
}
