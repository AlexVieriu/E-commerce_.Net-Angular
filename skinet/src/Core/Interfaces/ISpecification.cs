namespace Core.Interfaces;

// interface one
public interface ISpecification<T>
{
    bool IsDistinct { get; }
    bool IsPagingEnabled { get; }
    int Skip { get; }
    int Take { get; }
    Expression<Func<T, bool>>? Criteria { get; }
    Expression<Func<T, object>>? OrderByAsc { get; }
    Expression<Func<T, object>>? OrderByDesc { get; }
    IQueryable<T> ApplyCriteria(IQueryable<T> query);
    List<Expression<Func<T, object>>> Includes { get; }
}

// interface 2
public interface ISpecification<T, TResult> : ISpecification<T>
{
    Expression<Func<T, TResult>>? Select { get; }
}