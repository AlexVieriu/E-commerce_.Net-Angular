using System.ComponentModel.DataAnnotations;
using Application.Common.Validators;
using FluentValidation.TestHelper;

namespace Application.Tests.Common.DTOs;

// Helper to run DataAnnotation validation without ASP.NET pipeline
// (still used by RegisterDTO, which keeps DataAnnotations — only CreateProductDTO was migrated).
file static class Validator
{
    public static IList<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        var ctx = new ValidationContext(model);
        System.ComponentModel.DataAnnotations.Validator
            .TryValidateObject(model, ctx, results, validateAllProperties: true);
        return results;
    }

    public static bool IsValid(object model) => !Validate(model).Any();
}

// DataAnnotations validation (RegisterDTO still uses [Required], [EmailAddress], etc.).
public class RegisterDto_Validation_Tests
{
    private const string TestPassword = "Pa$$w0rd12"; // pragma: allowlist secret

    [Fact]
    public void RegisterDto_ShouldPass_WhenAllFieldsAreValid()
    {
        var dto = new RegisterDTO
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Password = TestPassword
        };

        Validator.IsValid(dto).Should().BeTrue();
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    [InlineData("@nodomain")]
    public void RegisterDto_ShouldFail_WhenEmailIsInvalid(string badEmail)
    {
        var dto = new RegisterDTO
        {
            FirstName = "John",
            LastName = "Doe",
            Email = badEmail,
            Password = TestPassword
        };

        var errors = Validator.Validate(dto);

        errors.Should().Contain(r => r.MemberNames.Contains("Email"));
    }

    [Fact]
    public void RegisterDto_ShouldFail_WhenFirstNameIsEmpty()
    {
        var dto = new RegisterDTO
        {
            FirstName = "",
            LastName = "Doe",
            Email = "john@example.com",
            Password = TestPassword
        };

        var errors = Validator.Validate(dto);

        errors.Should().Contain(r => r.MemberNames.Contains("FirstName"));
    }

    [Fact]
    public void RegisterDto_ShouldFail_WhenPasswordIsEmpty()
    {
        var dto = new RegisterDTO
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Password = ""
        };

        var errors = Validator.Validate(dto);

        errors.Should().Contain(r => r.MemberNames.Contains("Password"));
    }
}

// FluentValidation (CreateProductDtoValidator + TestHelper's TestValidate).
public class CreateProductDto_Validation_Tests
{
    private readonly CreateProductDtoValidator _validator = new();

    private static CreateProductDTO ValidDto() => new()
    {
        Name = "Angular Board",
        Description = "Fast board",
        Price = 99.99m,
        PictureUrl = "/images/board.png",
        Type = "Boards",
        Brand = "Angular",
        QuantityInStock = 10
    };

    [Fact]
    public void CreateProductDto_ShouldPass_WhenAllFieldsAreValid()
    {
        _validator.TestValidate(ValidDto()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-99.99)]
    public void CreateProductDto_ShouldFail_WhenPriceIsNotPositive(decimal badPrice)
    {
        var dto = ValidDto();
        dto.Price = badPrice;

        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Price);
    }

    [Fact]
    public void CreateProductDto_ShouldFail_WhenQuantityIsZero()
    {
        var dto = ValidDto();
        dto.QuantityInStock = 0;

        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.QuantityInStock);
    }

    [Fact]
    public void CreateProductDto_ShouldFail_WhenNameIsEmpty()
    {
        var dto = ValidDto();
        dto.Name = "";

        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void CreateProductDto_ToEntity_ShouldMapAllFields()
    {
        var dto = new CreateProductDTO
        {
            Name = "Angular Board",
            Description = "Fast board",
            Price = 99.99m,
            PictureUrl = "/images/board.png",
            Type = "Boards",
            Brand = "Angular",
            QuantityInStock = 10
        };

        var product = dto.ToEntity();

        product.Name.Should().Be("Angular Board");
        product.Description.Should().Be("Fast board");
        product.Price.Should().Be(99.99m);
        product.PictureUrl.Should().Be("/images/board.png");
        product.Type.Should().Be("Boards");
        product.Brand.Should().Be("Angular");
        product.QuantityInStock.Should().Be(10);
    }

    [Fact]
    public void CreateProductDto_ToEntityWithId_ShouldSetId()
    {
        var dto = new CreateProductDTO
        {
            Name = "Hat",
            Description = "desc",
            Price = 10m,
            PictureUrl = "/img.png",
            Type = "Hats",
            Brand = "Core",
            QuantityInStock = 5
        };

        var product = dto.ToEntity(42);

        product.Id.Should().Be(42);
        product.Name.Should().Be("Hat");
    }
}
