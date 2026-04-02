namespace Infrastructure.Data;

public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
{
    // why we use protected field and not use directly the primary constructor in the declaration :
    // https://claude.ai/chat/69880ae7-fdd6-44d3-89f0-4a1c1809e65f
    // the only thing we change is from private to protected, so that the ChildClass(ProductRepository) can use it
    protected readonly StoreContext context;

    public GenericRepository(StoreContext context)
    {
        this.context = context;
    }

    public void Add(T entity)
        => context.Set<T>().Add(entity);

    public void Delete(T entity)
        => context.Set<T>().Remove(entity);

    public bool Exists(int id)
        => context.Set<T>().Any(x => x.Id == id);

    public void Update(T entity)
    {
        context.Set<T>().Attach(entity);
        context.Entry(entity).State = EntityState.Modified;
    }

    public async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await context.Set<T>().FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.Set<T>().ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(ISpecification<T> spec, CancellationToken cancellationToken = default)
    {
        return await ApplySpecification(spec).ToListAsync(cancellationToken);
    }

    public async Task<T?> GetEntityWithSpec(ISpecification<T> spec)
    {
        return await ApplySpecification(spec).FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<TResult>> GetAllAsync<TResult>(ISpecification<T, TResult> spec)
    {
        return await ApplySpecification(spec).ToListAsync();
    }

    public async Task<TResult?> GetEntityWithSpec<TResult>(ISpecification<T, TResult> spec)
    {
        return await ApplySpecification(spec).FirstOrDefaultAsync();
    }

    public async Task<(IReadOnlyList<T> Items, int TotalCount)> GetPagedAsync(ISpecification<T> spec)
    {
        var count = await CountAsync(spec);
        var items = await ApplySpecification(spec).ToListAsync();

        return (items, count);
    }

    private async Task<int> CountAsync(ISpecification<T> spec)
    {
        var query = context.Set<T>().AsQueryable();

        query = spec.ApplyCriteria(query);

        return await query.CountAsync();
    }

    // - - - - - -  PRIVATE HELPERS - - - - - -
    private IQueryable<T> ApplySpecification(ISpecification<T> spec)
    {
        return SpecificationEvaluator<T>.GetQuery(context.Set<T>().AsQueryable(), spec);
    }

    private IQueryable<TResult> ApplySpecification<TResult>(ISpecification<T, TResult> spec)
    {
        return SpecificationEvaluator<T>.GetQuery<T, TResult>(context.Set<T>().AsQueryable(), spec);
    }
}
