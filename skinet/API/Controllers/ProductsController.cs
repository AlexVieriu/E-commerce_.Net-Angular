namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController(IGenericRepository<Product> productRepo) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts(
        string? brand, string? type, string? sort)
    {
        var spec = new ProductSpecification(brand, type, sort);

        var products = await productRepo.GetAllAsync(spec);

        return Ok(products);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Product>> GetProduct(int id)
    {
        var product = await productRepo.GetByIdAsync(id);

        if (product == null)
            return NotFound();

        return Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<Product>> CreateProduct(Product product)
    {
        productRepo.Add(product);
        if (await productRepo.SaveAllAsync())
            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        else
            return BadRequest();
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Product>> UpdateProduct(int id, Product product)
    {
        var exists = await productRepo.ExistsAsync(id);
        if (id != product.Id || !exists)
            return BadRequest("Can't update product");

        productRepo.Update(product);

        if (await productRepo.SaveAllAsync())
            return Ok(product);
        else
            return BadRequest("Problem updating the product");
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteProduct(int id)
    {
        var product = await productRepo.GetByIdAsync(id);

        if (product == null)
            return NotFound();

        productRepo.Delete(product);
        if (await productRepo.SaveAllAsync())
            return NoContent();
        else
            return BadRequest();
    }

    [HttpGet("brands")]
    public async Task<ActionResult<IEnumerable<string>>> GetBrandsAsync()
    {
        // TODO: Implement method

        return Ok();
    }

    [HttpGet("types")]
    public async Task<ActionResult<IEnumerable<string>>> GetTypesAsync()
    {
        // TODO: Implement method

        return Ok();
    }
}