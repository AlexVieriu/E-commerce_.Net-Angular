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
            // Why need .Split: https://claude.ai/chat/4f11d906-a3bd-46cb-b662-f86d9210e47e
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
        // Invariant, not current-culture: this runs in .NET, and ToLower() would use the server's
        // locale — in Turkish, "I".ToLower() is "ı" (dotless), so a search for "IPHONE" would miss.
        set => _search = value.ToLowerInvariant();
    }

}
