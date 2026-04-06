using Application.Specifications;

namespace Application.Tests.Specification;

// BEGINNER EXAMPLE 11: Testing Default Values
public class PagingParams_Tests
{
    [Fact]
    public void PagingParams_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var pagingParams = new PagingParams();

        // Assert
        pagingParams.PageIndex.Should().Be(1);
        pagingParams.PageSize.Should().Be(6); ;
    }

    [Fact]
    public void PagingParams_PageSize_ShouldNotExceedMaximum()
    {
        // Arrange & Act
        var pagingParams = new PagingParams() { PageSize = 100 };

        // Assert
        pagingParams.PageSize.Should().Be(60);
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(20, 20)]
    [InlineData(75, 60)]
    [InlineData(999, 60)]
    public void PagingParams_PageSize_ShouldRespectMaximum(int pageSize, int expectedPageSize)
    {
        // Arrange & Act
        var pagingParams = new PagingParams() { PageSize = pageSize };

        // Assert 
        pagingParams.PageSize.Should().Be(expectedPageSize);
    }
}
