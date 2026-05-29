using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Desktop.Common;

/// Centralized Vietnamese display names for domain enums and string-typed values shown in UI.
/// Adding a new enum? Map every value here so the converter never falls back to the raw English name.
public static class EnumDisplay
{
    public static string ProjectStatus(ProjectStatus s) => s switch
    {
        Domain.Enums.ProjectStatus.Planning => "Đang lập kế hoạch",
        Domain.Enums.ProjectStatus.InProgress => "Đang thi công",
        Domain.Enums.ProjectStatus.OnHold => "Tạm dừng",
        Domain.Enums.ProjectStatus.Completed => "Hoàn thành",
        Domain.Enums.ProjectStatus.Cancelled => "Đã hủy",
        _ => s.ToString()
    };

    public static string PaymentType(PaymentType t) => t switch
    {
        Domain.Enums.PaymentType.Advance => "Tạm ứng",
        Domain.Enums.PaymentType.Installment => "Thanh toán đợt",
        Domain.Enums.PaymentType.FinalSettlement => "Quyết toán",
        Domain.Enums.PaymentType.RetentionRelease => "Giải tỏa bảo lãnh",
        _ => t.ToString()
    };

    public static string AttachmentEntityType(AttachmentEntityType t) => t switch
    {
        Domain.Enums.AttachmentEntityType.ApprovalDecision => "Quyết định phê duyệt",
        Domain.Enums.AttachmentEntityType.Contract => "Hợp đồng",
        Domain.Enums.AttachmentEntityType.Project => "Dự án",
        Domain.Enums.AttachmentEntityType.CostItem => "Đầu mục chi phí",
        Domain.Enums.AttachmentEntityType.Payment => "Thanh toán",
        _ => t.ToString()
    };

    public static string AuditAction(AuditAction a) => a switch
    {
        Domain.Enums.AuditAction.Create => "Tạo mới",
        Domain.Enums.AuditAction.Update => "Cập nhật",
        Domain.Enums.AuditAction.Delete => "Xóa",
        _ => a.ToString()
    };

    /// Backup types are persisted as strings, not enums. Map the well-known values.
    public static string BackupType(string? type) => type switch
    {
        "manual" => "Thủ công",
        "on_exit" => "Tự động khi đóng app",
        "pre_restore" => "Tự động trước khi restore",
        "scheduled" => "Tự động theo lịch",
        null or "" => "(không rõ)",
        _ => type
    };

    public static string Localize(object? value) => value switch
    {
        ProjectStatus ps => ProjectStatus(ps),
        PaymentType pt => PaymentType(pt),
        AttachmentEntityType at => AttachmentEntityType(at),
        AuditAction aa => AuditAction(aa),
        string s => s,
        null => string.Empty,
        _ => value.ToString() ?? string.Empty
    };
}
