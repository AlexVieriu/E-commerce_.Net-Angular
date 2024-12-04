namespace Core.Interfaces;

public interface IGenericRepository<T> where T : BaseEntity
{
    Task<bool> ExistsAsync(int id);
    Task<bool> SaveAllAsync();
    Task<IReadOnlyList<T>> GetAllAsync();
    Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec);
    Task<T?> GetByIdAsync(int id);
    Task<T?> GetEntityWithSpec(ISpecification<T> spec);
    void Add(T entity);
    void Delete(T entity);
    void Update(T entity);
}
