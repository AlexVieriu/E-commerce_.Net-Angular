namespace Infrastructure.Data;

public class GenericRepository<T>(StoreContext context) : IGenericRepository<T>
    where T : BaseEntity
{
    // we make the methods that interact directly with the database async, the rest are sync so we don't block any thread
    // the ones that are sync are saved on the context
    // SaveChangesAsync() will take all the changes on the context, and execute the queries to the DB
    public void Add(T entity)
    {
        context.Set<T>().Add(entity);
    }

    public async Task<int> CountAsync(ISpecification<T> spec)
    {
        var query = context.Set<T>().AsQueryable();

        query = spec.ApplyCriteria(query);

        return await query.CountAsync();
    }

    public void Delete(T entity)
    {
        context.Set<T>().Remove(entity);
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await context.Set<T>().AnyAsync(x => x.Id == id);
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(ISpecification<T> spec)
    {
        // return await context.Set<T>().Where(x =>
        // (string.IsNullOrWhiteSpace(brand) || x.Brand == brand) &&
        // (string.IsNullOrWhiteSpace(type) || x.Type == type)).ToListAsync();

        return await ApplySpecification(spec).ToListAsync();
    }

    public async Task<IReadOnlyList<TResult>> GetAllAsync<TResult>(ISpecification<T, TResult> spec)
    {
        return await ApplySpecification(spec).ToListAsync();
    }

    public async Task<T?> GetByIdAsync(int id)
    {
        return await context.Set<T>().FindAsync(id);
    }

    public async Task<T?> GetEntityWithSpec(ISpecification<T> spec)
    {
        return await ApplySpecification(spec).FirstOrDefaultAsync();
    }

    public async Task<TResult?> GetEntityWithSpec<TResult>(ISpecification<T, TResult> spec)
    {
        return await ApplySpecification(spec).FirstOrDefaultAsync();
    }

    public async Task<bool> SaveAllAsync()
    {
        return await context.SaveChangesAsync() > 0;
    }

    public void Update(T entity)
    {
        context.Set<T>().Attach(entity);
        context.Entry(entity).State = EntityState.Modified;
    }

    private IQueryable<T> ApplySpecification(ISpecification<T> spec)
    {
        return SpecificationEvaluator<T>.GetQuery(context.Set<T>().AsQueryable(), spec);
    }

    private IQueryable<TResult> ApplySpecification<TResult>(ISpecification<T, TResult> spec)
    {
        return SpecificationEvaluator<T>.GetQuery<T, TResult>(context.Set<T>().AsQueryable(), spec);
    }
}
