namespace Application.Specifications;

public class ProductSpecification : BaseSpecification<Product>
{
    // Don't "fix" CA1304/CA1311/CA1862 here: this lambda becomes SQL, and EF throws on StringComparison overloads.
    // ToLower() is fine — it translates to SQL LOWER(), run by the database's collation.
    // https://learn.microsoft.com/en-us/ef/core/miscellaneous/collations-and-case-sensitivity
    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "EF expression tree — runs as SQL, not .NET.")]
    [SuppressMessage("Globalization", "CA1311:Specify a culture", Justification = "EF expression tree — runs as SQL, not .NET.")]
    [SuppressMessage("Performance", "CA1862:Use StringComparison overload", Justification = "EF cannot translate StringComparison overloads — it throws.")]
    // An empty filter makes its clause always true, so EF drops it from the SQL.
    public ProductSpecification(ProductSpecParams specParams)
        : base(x => (string.IsNullOrEmpty(specParams.Search) || x.Name.ToLower().Contains(specParams.Search)) &&
                    (specParams.Brands.Count == 0 || specParams.Brands.Contains(x.Brand)) &&
                    (specParams.Types.Count == 0 || specParams.Types.Contains(x.Type)))
    {
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
