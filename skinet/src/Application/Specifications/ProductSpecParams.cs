namespace Application.Specifications;

public class ProductSpecParams : PagingParams
{
    private List<string> _brands = [];
    private List<string> _types = [];
    public List<string> Brands
    {
        get => _brands;
        set
        {
            // The client sends ?brands=a,b as one value; split it so each brand is its own item (same for Types).
            _brands = value.SelectMany(x => x.Split(',',
                StringSplitOptions.RemoveEmptyEntries)).ToList();
        }
    }

    public List<string> Types
    {
        get => _types;
        set
        {
            _types = value.SelectMany(x => x.Split(',',
                StringSplitOptions.RemoveEmptyEntries)).ToList();
        }
    }

    public string? Sort { get; set; }

    private string? _search;
    public string Search
    {
        get => _search ?? "";
        // Invariant: ToLower() follows the server's locale, and in Turkish "I" becomes "ı".
        set => _search = value.ToLowerInvariant();
    }

}
