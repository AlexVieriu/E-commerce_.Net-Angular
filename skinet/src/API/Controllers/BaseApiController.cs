namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BaseApiController : ControllerBase
{
    protected async Task<ActionResult> CreatePagedResult<T>(IGenericRepository<T> repo,
                                                            ISpecification<T> spec,
                                                            int pageIndex,
                                                            int pageSize)
        where T : BaseEntity
    {
        var (items, count) = await repo.GetPagedAsync(spec);
        var pagination = new Pagination<T>(pageIndex, pageSize, count, items);

        return Ok(pagination);
    }

    protected async Task<ActionResult> CreatePagedResult<T, TDto>(IGenericRepository<T> repo,
                                                                  ISpecification<T> spec,
                                                                  int pageIndex,
                                                                  int pageSize,
                                                                  Func<T, TDto> toDto)
        where T : BaseEntity
    {
        var (items, count) = await repo.GetPagedAsync(spec);
        var dtoItems = items.Select(toDto).ToList();
        var pagination = new Pagination<TDto>(pageIndex, pageSize, count, dtoItems);

        return Ok(pagination);
    }
}