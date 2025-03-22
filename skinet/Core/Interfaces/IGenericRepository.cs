namespace Core.Interfaces;
public interface IGenericRepository<T> where T : BaseEntity
{
    Task<int> CountAsync(ISpecification<T> spec);
    Task<IReadOnlyList<T>> GetAllAsync();
    Task<IReadOnlyList<T>> GetAllAsync(ISpecification<T> spec);
    Task<IReadOnlyList<TResult>> GetAllAsync<TResult>(ISpecification<T, TResult> spec);
    Task<T?> GetByIdAsync(int id);
    Task<T?> GetEntityWithSpec(ISpecification<T> spec);
    Task<TResult?> GetEntityWithSpec<TResult>(ISpecification<T, TResult> spec);
    void Add(T entity);
    void Delete(T entity);
    void Update(T entity);
    bool Exists(int id);
}
