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

    public void Delete(T entity)
    {
        context.Set<T>().Remove(entity);
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await context.Set<T>().AnyAsync(x => x.Id == id);
    }

    public async Task<IReadOnlyList<T>> GetAllAsync()
    {
        return await context.Set<T>().ToListAsync();
    }

    public async Task<T?> GetByIdAsync(int id)
    {
        return await context.Set<T>().FindAsync(id);
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
}
