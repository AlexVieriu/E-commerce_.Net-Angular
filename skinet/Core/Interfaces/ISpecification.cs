namespace Core.Interfaces;

// interface one
public interface ISpecification<T>
{
    bool IsDistinct { get; }
    bool IsPagingEnabled { get; }
    int Skip { get; }
    int Take { get; }
    Expression<Func<T, bool>>? Criteria { get; }
    Expression<Func<T, object>>? OrderBy { get; }
    Expression<Func<T, object>>? OrderByDescending { get; }
    IQueryable<T> ApplyCriteria(IQueryable<T> query);
    List<Expression<Func<T, object>>> Includes { get; }
    List<string> IncludeStrings { get; } // for ThenInclude
}

// interface 2
public interface ISpecification<T, TResult> : ISpecification<T>
{
    Expression<Func<T, TResult>>? Select { get; }
}