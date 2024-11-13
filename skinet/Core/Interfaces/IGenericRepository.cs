namespace Core.Interfaces;

public interface IGenericRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id);
    Task<IReadOnlyList<T>> GetAllAsync();
    void Add(T entity);
    void Update(T entity);
    void Delete(T entity);
    Task<bool> SaveAllAsync();
    Task<bool> ExistsAsync(int id);
}
