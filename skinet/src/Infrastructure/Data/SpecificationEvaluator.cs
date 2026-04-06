namespace Infrastructure.Data;

public class SpecificationEvaluator<T> where T : BaseEntity
{
    public static IQueryable<T> GetQuery(IQueryable<T> query, ISpecification<T> spec)
    {
        // Here we create the query. The order matter !!!       

        // WHERE - Filter first to reduce dataset calls
        if (spec.Criteria != null)
            query = query.Where(spec.Criteria);

        // Eager loading : https://claude.ai/chat/d2ef917a-1dc8-451f-a280-48bcb3a055b9
        // if (spec.Includes.Count > 1)
        //     query = query.AsSplitQuery();

        // Here we do all the JOINs :
        /*
            SELECT o.*, oi.*, b.*
            FROM Orders o
            LEFT JOIN OrderItems oi ON o.Id = oi.OrderId
            LEFT JOIN Buyers b ON o.BuyerId = b.Id
            WHERE o.BuyerEmail = 'test@test.com'
        */
        query = spec.Includes.Aggregate(query, (current, include) => current.Include(include));

        if (spec.OrderByAsc != null)
            query = query.OrderBy(spec.OrderByAsc);
        else if (spec.OrderByDesc != null)
            query = query.OrderByDescending(spec.OrderByDesc);

        if (spec.IsDistinct)
            query = query.Distinct();

        if (spec.IsPagingEnabled)
            query = query.Skip(spec.Skip).Take(spec.Take);

        // How the query will look like now:
        /*
            SELECT o.*, oi.*, b.*
            FROM Orders o
            INNER JOIN OrderItems oi ON o.Id = oi.OrderId
            INNER JOIN Buyers b ON o.BuyerId = b.Id
            WHERE o.BuyerEmail = 'test@test.com'
            ORDER BY o.OrderDate DESC
            OFFSET 0 ROWS
            FETCH NEXT 10 ROWS ONLY
        */

        return query;
    }

    public static IQueryable<TResult> GetQuery<TSpec, TResult>(IQueryable<T> query,
                                                               ISpecification<T, TResult> spec)
    {
        if (spec.Criteria != null)
            query = query.Where(spec.Criteria);

        query = spec.Includes.Aggregate(query, (current, include) => current.Include(include));

        if (spec.OrderByAsc != null)
            query = query.OrderBy(spec.OrderByAsc);
        else if (spec.OrderByDesc != null)
            query = query.OrderByDescending(spec.OrderByDesc);

        var projectedQuery = spec.Select != null
            ? query.Select(spec.Select)
            : query.Cast<TResult>();

        if (spec.IsDistinct)
            projectedQuery = projectedQuery.Distinct();

        if (spec.IsPagingEnabled)
            projectedQuery = projectedQuery.Skip(spec.Skip).Take(spec.Take);

        return projectedQuery;
    }
}
