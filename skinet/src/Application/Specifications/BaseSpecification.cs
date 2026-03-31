namespace Application.Specifications;

// Class 1
public class BaseSpecification<T> : ISpecification<T>
{
    private Expression<Func<T, bool>>? _criteria;

    protected BaseSpecification() { }

    protected BaseSpecification(Expression<Func<T, bool>>? criteria)
    {
        _criteria = criteria;
    }

    // Where clause
    public Expression<Func<T, bool>>? Criteria => _criteria;
    public Expression<Func<T, object>>? OrderByAsc { get; protected set; }
    public Expression<Func<T, object>>? OrderByDesc { get; protected set; }

    // JOIN / INNER JOIN 
    public List<Expression<Func<T, object>>> Includes { get; } = [];

    public int Take { get; private set; }
    public int Skip { get; private set; }
    public bool IsPagingEnabled { get; private set; }
    public bool IsDistinct { get; private set; }

    // Where Clause
    public IQueryable<T> ApplyCriteria(IQueryable<T> query)
    {
        if (_criteria != null)
            query = query.Where(_criteria);

        return query;
    }

    protected void AddCriteria(Expression<Func<T, bool>> criteria)
        => _criteria = _criteria == null ? criteria : CombineWithAnd(_criteria, criteria);

    protected void AddInclude(Expression<Func<T, object>> includeExpression)
        => Includes.Add(includeExpression);

    protected void AddOrderByAsc(Expression<Func<T, object>> orderByExpression)
        => OrderByAsc = orderByExpression;

    protected void AddOrderByDescending(Expression<Func<T, object>> orderByDescendingExpression)
        => OrderByDesc = orderByDescendingExpression;

    protected void ApplyDistinct()
        => IsDistinct = true;

    protected void ApplyPaging(int skip, int take)
    {
        if (skip < 0)
            throw new ArgumentOutOfRangeException(nameof(skip), "Skip must be non-negative");

        if (take <= 0)
            throw new ArgumentOutOfRangeException(nameof(take), "Take must be positive");

        Skip = skip;
        Take = take;
        IsPagingEnabled = true;
    }

    // - - - PRIVATE HELPER - - - 
    private Expression<Func<T, bool>> CombineWithAnd(Expression<Func<T, bool>> left,
                                                     Expression<Func<T, bool>> right)
    {
        var parameter = Expression.Parameter(typeof(T));

        var leftVisitor = new ReplaceParameterVisitor(left.Parameters[0], parameter);
        var leftBody = leftVisitor.Visit(left.Body);

        var rightVisitor = new ReplaceParameterVisitor(right.Parameters[0], parameter);
        var rightBody = rightVisitor.Visit(right.Body);

        var combined = Expression.AndAlso(leftBody!, rightBody!);

        return Expression.Lambda<Func<T, bool>>(combined, parameter);
    }

    // ExpressionVisitor: a visitor or rewriter for expression trees
    private class ReplaceParameterVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _oldParameter;
        private readonly ParameterExpression _newParameter;

        public ReplaceParameterVisitor(ParameterExpression oldParameter, ParameterExpression newParameter)
        {
            _oldParameter = oldParameter;
            _newParameter = newParameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _oldParameter ? _newParameter : base.VisitParameter(node);
        }
    }
}

// Class 2 
public class BaseSpecification<T, TResult> : BaseSpecification<T>, ISpecification<T, TResult>
{
    protected BaseSpecification() : base() { }

    protected BaseSpecification(Expression<Func<T, bool>>? criteria) : base(criteria) { }

    public Expression<Func<T, TResult>>? Select { get; private set; }

    /// <summary>
    /// Project query results to a different type (SELECT Name, Price FROM Products)
    /// </summary>
    protected void AddSelect(Expression<Func<T, TResult>> selectExpression)
    {
        Select = selectExpression;
    }
}