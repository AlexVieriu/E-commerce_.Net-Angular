namespace Infrastructure.Data;

public class SpecificationEvaluator<T> where T : BaseEntity
{
    // Pipeline order matters: WHERE narrows the row set BEFORE JOINs and ORDER BY run.
    // EF emits INNER JOIN for required navs (e.g. Order.DeliveryMethod) and
    // LEFT JOIN for collection navs (e.g. Order.OrderItems).
    public static IQueryable<T> GetQuery(IQueryable<T> query, ISpecification<T> spec)
    {
        query = ApplyCommon(query, spec);

        if (spec.IsDistinct)
            query = query.Distinct();

        if (spec.IsPagingEnabled)
            query = query.Skip(spec.Skip).Take(spec.Take);

        return query;
    }

    public static IQueryable<TResult> GetQuery<TSpec, TResult>(IQueryable<T> query,
                                                               ISpecification<T, TResult> spec)
    {
        query = ApplyCommon(query, spec);

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

        query = spec.Includes.Aggregate(query, (current, include) => current.Include(include));

        if (spec.OrderByAsc != null)
            query = query.OrderBy(spec.OrderByAsc);
        else if (spec.OrderByDesc != null)
            query = query.OrderByDescending(spec.OrderByDesc);

        return query;
    }
}
