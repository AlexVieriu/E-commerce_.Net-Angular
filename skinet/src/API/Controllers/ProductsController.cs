namespace API.Controllers;

public class ProductsController(IUnitOfWork unitOfWork) : BaseApiController
{
    // Matches list keys ("/api/products|…") and by-id keys ("/api/products/72").
    // A trailing "|" would miss the by-id keys and leave the edit view stale (BUG-15).
    private const string ProductsCachePattern = "api/products";

    [Cache(10)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts(
        [FromQuery] ProductSpecParams specParams)
    {
        var spec = new ProductSpecification(specParams);

        return await CreatePagedResult(
            unitOfWork.Repository<Product>(), spec, specParams.PageIndex, specParams.PageSize);
    }

    [Cache(10)]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Product>> GetProductById(int id)
    {
        var product = await unitOfWork.Repository<Product>().GetByIdAsync(id);

        if (product == null)
            return NotFound();

        return Ok(product);
    }

    [InvalidateCache(ProductsCachePattern)]
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<Product>> CreateProduct(CreateProductDTO productDto)
    {
        var product = productDto.ToEntity();
        unitOfWork.Repository<Product>().Add(product);
        if (await unitOfWork.Complete())
            return CreatedAtAction(nameof(GetProductById), new { id = product.Id }, product);
        else
            return BadRequest();
    }

    [InvalidateCache(ProductsCachePattern)]
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<Product>> UpdateProduct(int id, CreateProductDTO productDto)
    {
        // Load the tracked entity: EF needs its original stock for the [ConcurrencyCheck] WHERE clause (BUG-16).
        var product = await unitOfWork.Repository<Product>().GetByIdAsync(id);

        if (product == null)
            return NotFound();

        productDto.MapTo(product);

        if (await unitOfWork.Complete())
            return Ok(product);
        else
            return BadRequest("Problem updating the product");
    }

    [InvalidateCache(ProductsCachePattern)]
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteProduct(int id)
    {
        var product = await unitOfWork.Repository<Product>().GetByIdAsync(id);

        if (product == null)
            return NotFound();

        unitOfWork.Repository<Product>().Delete(product);
        if (await unitOfWork.Complete())
            return NoContent();
        else
            return BadRequest();
    }

    [Cache(180)]
    [HttpGet("brands")]
    public async Task<ActionResult<IEnumerable<string>>> GetBrands()
    {
        var spec = new BrandListSpecification();

        return Ok(await unitOfWork.Repository<Product>().GetAllAsync(spec));
    }
    [Cache(180)]
    [HttpGet("types")]
    public async Task<ActionResult<IEnumerable<string>>> GetTypes()
    {
        var spec = new TypeListSpecification();

        return Ok(await unitOfWork.Repository<Product>().GetAllAsync(spec));
    }
}