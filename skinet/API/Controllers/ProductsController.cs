namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController(IProductRepository productRepo) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts(
        string? brand, string? type, string? sort)
    {
        var products = await productRepo.GetProductAsync(brand, type, sort);

        return Ok(products);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Product>> GetProduct(int id)
    {
        var product = await productRepo.GetProductByIdAsync(id);

        if (product == null)
            return NotFound();

        return Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<Product>> CreateProduct(Product product)
    {
        productRepo.AddProduct(product);
        if (await productRepo.SaveChangesAsync())
            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        else
            return BadRequest();
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Product>> UpdateProduct(int id, Product product)
    {
        if (id != product.Id || !productRepo.ProductExists(id))
            return BadRequest("Can't update product");

        productRepo.UpdateProduct(product);

        if (await productRepo.SaveChangesAsync())
            return Ok(product);
        else
            return BadRequest("Problem updating the product");
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteProduct(int id)
    {
        var product = await productRepo.GetProductByIdAsync(id);

        if (product == null)
            return NotFound();

        productRepo.DeleteProduct(product);
        if (await productRepo.SaveChangesAsync())
            return NoContent();
        else
            return BadRequest();
    }

    [HttpGet("brands")]
    public async Task<ActionResult<IEnumerable<string>>> GetBrandsAsync()
    {
        var products = await productRepo.GetBrandsAsync();
        if (products == null)
            return NotFound();

        return Ok(products);
    }

    [HttpGet("types")]
    public async Task<ActionResult<IEnumerable<string>>> GetTypesAsync()
    {
        var products = await productRepo.GetTypesAsync();
        if (products == null)
            return NotFound();

        return Ok(await productRepo.GetTypesAsync());
    }
}