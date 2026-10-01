namespace ConstructionFinance.Application.Printing;

public enum PrintOrientation { Portrait, Landscape }

/// Căn trang của một sheet. FitToPagesWide = 1 nghĩa là ép vừa đúng một trang bề
/// ngang; FitToPagesTall = 0 nghĩa là dọc bao nhiêu trang cũng được.
public sealed record PrintPageProfile(
    PrintOrientation Orientation,
    int FitToPagesWide,
    int FitToPagesTall,
    int RepeatRowsTop,
    int RepeatColsLeft);

/// Sáu hồ sơ căn trang, gom một chỗ để không trôi khỏi nhau. Xem spec §5.
public static class PrintPageProfiles
{
    /// Sổ Vị trí: 14 cột nên phải ngang; lặp dòng tiêu đề + dòng TỔNG CỘNG, và lặp
    /// hai cột trái (Ngày, Nội dung) để trang sau còn biết dòng nào là dòng nào.
    public static PrintPageProfile ViTriLedger { get; } =
        new(PrintOrientation.Landscape, 1, 0, 2, 2);

    public static PrintPageProfile ProjectTongHop { get; } =
        new(PrintOrientation.Landscape, 1, 0, 1, 2);

    /// Chỉ 4 cột — để dọc, ngang sẽ thừa nửa tờ giấy.
    public static PrintPageProfile YearSettlement { get; } =
        new(PrintOrientation.Portrait, 1, 0, 1, 0);

    // Ba biểu mẫu cũng chỉ ép vừa bề ngang như mọi hồ sơ khác (spec §5 chỉ có cột "Ép ngang").
    // Ép luôn chiều dọc thì Excel thu nhỏ vô hạn: một Thông tri dài co đến không đọc nổi thay vì
    // sang trang — hai trang đọc được hơn một trang không đọc được.
    public static PrintPageProfile ThongTri { get; } = new(PrintOrientation.Portrait, 1, 0, 0, 0);
    public static PrintPageProfile KsChi { get; } = new(PrintOrientation.Portrait, 1, 0, 0, 0);
    public static PrintPageProfile Gdntt { get; } = new(PrintOrientation.Landscape, 1, 0, 0, 0);
}
