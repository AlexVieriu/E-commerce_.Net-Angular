namespace Application.Specifications;

public class ProductSpecification : BaseSpecification<Product>
{
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
                AddOrderByAsc(p => (object)p.Price);
                break;
            case "priceDesc":
                AddOrderByDescending(p => (object)p.Price);
                break;
            default:
                AddOrderByAsc(p => p.Name);
                break;
        }
    }
}