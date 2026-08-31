namespace Application.Specifications;

public class ProductSpecification : BaseSpecification<Product>
{
    // CA1304/CA1311/CA1862 want StringComparison.OrdinalIgnoreCase here. Do NOT apply them:
    // this lambda is an expression tree that EF translates to SQL, and EF "refrains from translating
    // these overloads to SQL, and attempting to use them will result in an exception".
    // https://learn.microsoft.com/en-us/ef/core/miscellaneous/collations-and-case-sensitivity
    // ToLower() is fine — it becomes SQL LOWER(), evaluated by the database's collation, not .NET's culture.
    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "EF expression tree — runs as SQL, not .NET.")]
    [SuppressMessage("Globalization", "CA1311:Specify a culture", Justification = "EF expression tree — runs as SQL, not .NET.")]
    [SuppressMessage("Performance", "CA1862:Use StringComparison overload", Justification = "EF cannot translate StringComparison overloads — it throws.")]
    // if there are no filters(==0), or contain filers, apply OrderBy
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