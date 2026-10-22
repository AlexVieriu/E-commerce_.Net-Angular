namespace Infrastructure.Data;

// A non-generic class with generic methods, so T is inferred at the call site (CA1000).
public static class SpecificationEvaluator
{
    // Distinct runs before Skip/Take (dedupe, then page), and paging relies on ApplyCommon's ORDER BY.
    // The order of Where/Include/OrderBy calls doesn't affect speed — the database picks the plan.
    public static IQueryable<T> GetQuery<T>(IQueryable<T> query, ISpecification<T> spec)
        where T : BaseEntity
    {
        query = ApplyCommon(query, spec);

        if (spec.IsDistinct)
            query = query.Distinct();

        if (spec.IsPagingEnabled)
            query = query.Skip(spec.Skip).Take(spec.Take);

        return query;
    }

    public static IQueryable<TResult> GetQuery<T, TResult>(IQueryable<T> query,
                                                           ISpecification<T, TResult> spec)
        where T : BaseEntity
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

    private static IQueryable<T> ApplyCommon<T>(IQueryable<T> query, ISpecification<T> spec)
        where T : BaseEntity
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
