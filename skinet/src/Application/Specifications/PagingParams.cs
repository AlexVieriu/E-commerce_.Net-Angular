namespace Application.Specifications;

public class PagingParams
{
    private const int MaxPageSize = 60;
    private int _pageSize = 6;

    public int PageIndex { get; set; } = 1;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Min(value, MaxPageSize);
    }
}