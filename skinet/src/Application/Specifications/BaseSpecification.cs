namespace Application.Specifications;

// Holds a query described as data (Criteria, Includes, OrderBy, paging…). Runs nothing —
// the SpecificationEvaluator reads these properties and builds the EF query.
public class BaseSpecification<T> : ISpecification<T>
{
    private Expression<Func<T, bool>>? _criteria;

    // - - - CONSTRUCTORS - - -
    protected BaseSpecification() { }

    protected BaseSpecification(Expression<Func<T, bool>>? criteria)
    {
        _criteria = criteria;
    }

    // - - - PROPERTIES (the query as data — each maps to a SQL clause) - - -
    public Expression<Func<T, bool>>? Criteria => _criteria;                 // WHERE
    public Expression<Func<T, object>>? OrderByAsc { get; protected set; }   // ORDER BY ... ASC
    public Expression<Func<T, object>>? OrderByDesc { get; protected set; }  // ORDER BY ... DESC
    public List<Expression<Func<T, object>>> Includes { get; } = [];         // JOIN (eager-load navs)
    public int Skip { get; private set; }                                    // OFFSET (paging)
    public int Take { get; private set; }                                    // FETCH  (paging)
    public bool IsPagingEnabled { get; private set; }
    public bool IsDistinct { get; private set; }                             // DISTINCT

    // - - - BUILDER METHODS (subclasses call these to fill the spec) - - -
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

    // - - - CRITERIA MACHINERY (apply the WHERE; combine multiple AddCriteria calls with AND) - - -
    public IQueryable<T> ApplyCriteria(IQueryable<T> query)
    {
        if (_criteria != null)
            query = query.Where(_criteria);

        return query;
    }

    // Merge two predicates into one (left AND right). Each lambda has its own parameter,
    // so we rebind both to a single shared parameter — otherwise EF can't read the tree.
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

    // ExpressionVisitor that swaps one parameter node for another while walking the tree.
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

// Variant that also projects to a different shape — adds Select (SELECT col1, col2…) on top of BaseSpecification<T>.
public class BaseSpecification<T, TResult> : BaseSpecification<T>, ISpecification<T, TResult>
{
    protected BaseSpecification() : base() { }

    protected BaseSpecification(Expression<Func<T, bool>>? criteria) : base(criteria) { }

    public Expression<Func<T, TResult>>? Select { get; private set; }        // SELECT projection

    // Project query results to a different type, e.g. SELECT DISTINCT Brand FROM Products.
    protected void AddSelect(Expression<Func<T, TResult>> selectExpression)
        => Select = selectExpression;
}
