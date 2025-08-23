namespace Core.Specifications;

public class OrderSpecParams : PagingParams
{
    // admin user can filter by one status at a time
    public string? Status { get; set; } 
}
