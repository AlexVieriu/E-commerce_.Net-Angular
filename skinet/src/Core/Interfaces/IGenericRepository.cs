namespace Core.Interfaces;

public interface IGenericRepository<T> where T : BaseEntity
{
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<T>> GetAllAsync(ISpecification<T> spec, CancellationToken cancellationToken);
    Task<IReadOnlyList<TResult>> GetAllAsync<TResult>(ISpecification<T, TResult> spec);
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<T?> GetEntityWithSpec(ISpecification<T> spec);
    Task<TResult?> GetEntityWithSpec<TResult>(ISpecification<T, TResult> spec);
    Task<(IReadOnlyList<T> Items, int TotalCount)> GetPagedAsync(ISpecification<T> spec);
    bool Exists(int id);
    void Add(T entity);
    void Delete(T entity);
    void Update(T entity);
}
