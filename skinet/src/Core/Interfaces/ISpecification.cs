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
    // CA1716 flags `Select` because it's a reserved word in VB/F#, which would force those callers to
    // escape it. This solution is C#-only, and `Select` is exactly what this is — a LINQ projection.
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "C#-only solution; Select is the correct name for a projection.")]
    Expression<Func<T, TResult>>? Select { get; }
}