namespace Infrastructure.Data;

public class GenericRepository<T>(StoreContext context) : IGenericRepository<T> where T : BaseEntity
{
    // - - - - - -  WRITES (tracked in memory, persisted via UnitOfWork.Complete)  - - - - - -
    public void Add(T entity)
        => context.Set<T>().Add(entity);

    public void Update(T entity)
    {
        context.Set<T>().Attach(entity);
        context.Entry(entity).State = EntityState.Modified;
    }

    public void Delete(T entity)
        => context.Set<T>().Remove(entity);

    // - - - - - -  READ ONE  - - - - - -
    public bool Exists(int id)
        => context.Set<T>().Any(x => x.Id == id);

    public async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await context.Set<T>().FindAsync([id], cancellationToken);
    }

    public async Task<T?> GetEntityWithSpec(ISpecification<T> spec)
    {
        return await ApplySpecification(spec).FirstOrDefaultAsync();
    }

    public async Task<TResult?> GetEntityWithSpec<TResult>(ISpecification<T, TResult> spec)
    {
        return await ApplySpecification(spec).FirstOrDefaultAsync();
    }

    // - - - - - -  READ MANY  - - - - - -
    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.Set<T>().ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(ISpecification<T> spec, CancellationToken cancellationToken = default)
    {
        return await ApplySpecification(spec).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TResult>> GetAllAsync<TResult>(ISpecification<T, TResult> spec)
    {
        return await ApplySpecification(spec).ToListAsync();
    }

    public async Task<(IReadOnlyList<T> Items, int TotalCount)> GetPagedAsync(ISpecification<T> spec)
    {
        var count = await CountAsync(spec);
        var items = await ApplySpecification(spec).ToListAsync();

        return (items, count);
    }

    // - - - - - -  PRIVATE HELPERS  - - - - - -
    private IQueryable<T> ApplySpecification(ISpecification<T> spec)
    {
        return SpecificationEvaluator.GetQuery(context.Set<T>().AsQueryable(), spec);
    }

    private IQueryable<TResult> ApplySpecification<TResult>(ISpecification<T, TResult> spec)
    {
        return SpecificationEvaluator.GetQuery<T, TResult>(context.Set<T>().AsQueryable(), spec);
    }

    private async Task<int> CountAsync(ISpecification<T> spec)
    {
        var query = context.Set<T>().AsQueryable();

        query = spec.ApplyCriteria(query);

        return await query.CountAsync();
    }
}
