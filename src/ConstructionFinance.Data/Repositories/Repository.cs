using System.Linq.Expressions;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace ConstructionFinance.Data.Repositories;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext Context;
    protected readonly DbSet<T> DbSet;

    public Repository(AppDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) =>
        DbSet.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default) =>
        await DbSet.AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlyList<T>> ListIncludingDeletedAsync(CancellationToken ct = default) =>
        await DbSet.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        await DbSet.Where(predicate).AsNoTracking().ToListAsync(ct);

    public Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        DbSet.AnyAsync(predicate, ct);

    public Task AddAsync(T entity, CancellationToken ct = default) =>
        DbSet.AddAsync(entity, ct).AsTask();

    public void Update(T entity) => DbSet.Update(entity);

    public Task SoftDeleteAsync(T entity, CancellationToken ct = default)
    {
        if (entity is ISoftDeletable sd)
        {
            sd.DeletedAt = DateTime.UtcNow;
            var entry = Context.Entry(entity);
            if (entry.State == EntityState.Detached)
            {
                var tracked = DbSet.Local.FirstOrDefault(e => e.Id == entity.Id);
                if (tracked is ISoftDeletable trackedSd)
                {
                    trackedSd.DeletedAt = sd.DeletedAt;
                }
                else
                {
                    DbSet.Attach(entity);
                    Context.Entry(entity).State = EntityState.Modified;
                }
            }
            return Task.CompletedTask;
        }
        DbSet.Remove(entity);
        return Task.CompletedTask;
    }

    public void HardDelete(T entity) => DbSet.Remove(entity);
}
