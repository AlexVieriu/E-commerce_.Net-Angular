namespace Application.Specifications;

public class ProductSpecification : BaseSpecification<Product>
{
    // Each filter is added only when the user set it, so an unused filter never reaches the SQL.
    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "EF expression tree — runs as SQL, not .NET.")]
    [SuppressMessage("Globalization", "CA1311:Specify a culture", Justification = "EF expression tree — runs as SQL, not .NET.")]
    [SuppressMessage("Performance", "CA1862:Use StringComparison overload", Justification = "EF cannot translate StringComparison overloads — it throws.")]
    public ProductSpecification(ProductSpecParams specParams)
    {
        // Don't "fix" CA1304/CA1311/CA1862 below: this lambda becomes SQL, and EF throws on StringComparison overloads.
        // ToLower() is fine — it translates to SQL LOWER(), run by the database's collation.
        // https://learn.microsoft.com/en-us/ef/core/miscellaneous/collations-and-case-sensitivity
        if (!string.IsNullOrEmpty(specParams.Search))
            AddCriteria(x => x.Name.ToLower().Contains(specParams.Search));

        if (specParams.Brands.Count > 0)
            AddCriteria(x => specParams.Brands.Contains(x.Brand));

        if (specParams.Types.Count > 0)
            AddCriteria(x => specParams.Types.Contains(x.Type));

        ApplyPaging(specParams.PageSize * (specParams.PageIndex - 1), specParams.PageSize);

        switch (specParams.Sort)
        {
            case "priceAsc":
                AddOrderByAsc(p => p.Price);
                break;
            case "priceDesc":
                AddOrderByDescending(p => p.Price);
                break;
            default:
                AddOrderByAsc(p => p.Name);
                break;
        }
    }
}
