namespace Infrastructure.Data;

public class SpecificationEvaluator<T> where T : BaseEntity
{
    // ApplyCommon builds WHERE + JOINs + ORDER BY; this method adds the steps whose order changes the result.
    // Distinct must run before Skip/Take (dedupe, then page); paging leans on ApplyCommon's ORDER BY so the
    // page window is deterministic (SQL OFFSET/FETCH needs it). Chaining order of Where/Include/OrderBy is
    // NOT a perf lever — the DB optimizer picks the plan.
    public static IQueryable<T> GetQuery(IQueryable<T> query, ISpecification<T> spec)
    {
        query = ApplyCommon(query, spec);

        if (spec.IsDistinct)
            query = query.Distinct();

        if (spec.IsPagingEnabled)
            query = query.Skip(spec.Skip).Take(spec.Take);

        return query;
    }

    public static IQueryable<TResult> GetQuery<TResult>(IQueryable<T> query,
                                                        ISpecification<T, TResult> spec)
    {
        query = ApplyCommon(query, spec);

        // Project first, then Distinct/page operate on TResult, not the full entity row.
        var projectedQuery = spec.Select != null
            ? query.Select(spec.Select)
            : query.Cast<TResult>();

        if (spec.IsDistinct)
            projectedQuery = projectedQuery.Distinct();

        if (spec.IsPagingEnabled)
            projectedQuery = projectedQuery.Skip(spec.Skip).Take(spec.Take);

        return projectedQuery;
    }

    private static IQueryable<T> ApplyCommon(IQueryable<T> query, ISpecification<T> spec)
    {
        if (spec.Criteria != null)
            query = query.Where(spec.Criteria);

        // EF emits INNER JOIN for required navs (Order.DeliveryMethod), LEFT JOIN for collections (Order.OrderItems).
        query = spec.Includes.Aggregate(query, (current, include) => current.Include(include));

        if (spec.OrderByAsc != null)
            query = query.OrderBy(spec.OrderByAsc);
        else if (spec.OrderByDesc != null)
            query = query.OrderByDescending(spec.OrderByDesc);

        return query;
    }
}
