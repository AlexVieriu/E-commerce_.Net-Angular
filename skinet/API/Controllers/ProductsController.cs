namespace API.Controllers;

public class ProductsController(IUnitOfWork unitOfWork) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts(
        [FromQuery] ProductSpecParams specParams)
    {
        var spec = new ProductSpecification(specParams);

        return await CreatePagedResult(
            unitOfWork.Repository<Product>(), spec, specParams.PageIndex, specParams.PageSize);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Product>> GetProductById(int id)
    {
        var product = await unitOfWork.Repository<Product>().GetByIdAsync(id);

        if (product == null)
            return NotFound();

        return Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<Product>> CreateProduct(Product product)
    {
        unitOfWork.Repository<Product>().Add(product);
        if (await unitOfWork.Complete())
            return CreatedAtAction(nameof(GetProductById), new { id = product.Id }, product);
        else
            return BadRequest();
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Product>> UpdateProduct(int id, Product product)
    {
        var exists = unitOfWork.Repository<Product>().Exists(id);
        if (id != product.Id || !exists)
            return BadRequest("Can't update product");

        unitOfWork.Repository<Product>().Update(product);

        if (await unitOfWork.Complete())
            return Ok(product);
        else
            return BadRequest("Problem updating the product");
    }

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

    [HttpGet("brands")]
    public async Task<ActionResult<IEnumerable<string>>> GetBrands()
    {
        var spec = new BrandListSpecification();

        return Ok(await unitOfWork.Repository<Product>().GetAllAsync(spec));
    }

    [HttpGet("types")]
    public async Task<ActionResult<IEnumerable<string>>> GetTypes()
    {
        var spec = new TypeListSpecification();

        return Ok(await unitOfWork.Repository<Product>().GetAllAsync(spec));
    }
}