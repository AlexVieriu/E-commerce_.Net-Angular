namespace Infrastructure.Data;

public class UnitOfWork(StoreContext context) : IUnitOfWork
{
    private readonly ConcurrentDictionary<string, object> _repositories = new();

    // if we have 100 Entities, we will need to create 100 repositories, that's why 
    // we will create a generic repository
    public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity
    {
        // get's the name of the current member(at runtime)
        var type = typeof(TEntity).Name;

        return (IGenericRepository<TEntity>)_repositories.GetOrAdd(type, t =>
        {
            var repositoryType = typeof(GenericRepository<>).MakeGenericType(typeof(TEntity));

            return Activator.CreateInstance(repositoryType, context)
                ?? throw new InvalidOperationException(
                    $"Cannot create an instance of {repositoryType}");
        });
    }

    public async Task<bool> Complete()
    {
        return await context.SaveChangesAsync() > 0;
    }

    public void Dispose()
    {
        context.Dispose();
    }
}