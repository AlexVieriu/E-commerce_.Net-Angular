namespace Core.Interfaces;

// A query described as data: filter, includes, order and paging.
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

// The same, plus a projection to TResult.
public interface ISpecification<T, TResult> : ISpecification<T>
{
    // CA1716: `Select` is a VB/F# keyword, which doesn't matter in a C#-only solution.
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "C#-only solution; Select is the correct name for a projection.")]
    Expression<Func<T, TResult>>? Select { get; }
}