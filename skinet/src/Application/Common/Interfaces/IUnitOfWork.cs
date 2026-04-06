namespace Application.Common.Interfaces;
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    IGenericRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity;
    Task<bool> Complete();

}
