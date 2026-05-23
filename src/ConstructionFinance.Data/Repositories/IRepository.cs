using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Data.Repositories;

public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListIncludingDeletedAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    Task SoftDeleteAsync(T entity, CancellationToken ct = default);
    void HardDelete(T entity);
}
