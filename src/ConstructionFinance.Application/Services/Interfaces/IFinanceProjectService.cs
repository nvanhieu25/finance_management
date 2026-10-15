using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Services.Interfaces;

/// Sổ tài chính theo cây Dự án → Vị trí → Chi phí → Gói thầu → Đợt. Chỉ lưu/đọc cây; mọi phép tính nằm ở
/// TreeCalculator và các builder.
public interface IFinanceProjectService
{
    Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default);
    Task<Project?> GetTreeAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListTreesAsync(CancellationToken ct = default);
    Task ReplaceTreeAsync(Project project, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
