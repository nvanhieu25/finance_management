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

    // Ba biểu mẫu đều A4 DỌC như Mẫu.xlsx. Thông tri dài tuỳ số đợt nên chỉ ép vừa bề ngang (nhiều trang được: ép luôn
    // chiều dọc thì Excel tự thu nhỏ xuống tận 10%, sàn tỉ lệ của OOXML — hai trang đọc được hơn một trang không đọc nổi).
    // KS chi và GĐNTT có chiều cao cố định theo Mẫu nên ép luôn vừa MỘT trang: một giấy đề nghị tràn sang trang hai là
    // giấy không ký được. Bộ vẽ MigraDoc co thêm theo chiều dọc khi FitToPagesTall == 1 (MigraDocFormRenderer.ScaleOf).
    public static PrintPageProfile ThongTri { get; } = new(PrintOrientation.Portrait, 1, 0, 0, 0);
    public static PrintPageProfile KsChi { get; } = new(PrintOrientation.Portrait, 1, 1, 0, 0);
    public static PrintPageProfile Gdntt { get; } = new(PrintOrientation.Portrait, 1, 1, 0, 0);
}
