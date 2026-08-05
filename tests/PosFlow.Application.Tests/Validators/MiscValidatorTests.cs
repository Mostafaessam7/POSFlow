using PosFlow.Application.Auth;
using PosFlow.Application.Auth.Validators;
using PosFlow.Application.Products;
using PosFlow.Application.Products.Validators;
using PosFlow.Application.Shifts;
using PosFlow.Application.Shifts.Validators;
using Xunit;

namespace PosFlow.Application.Tests.Validators;

public sealed class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Theory]
    [InlineData("", "password123")]
    [InlineData("username", "")]
    [InlineData("", "")]
    public void Validate_WithMissingFields_IsInvalid(
        string username,
        string password)
    {
        var result = _validator.Validate(new LoginRequest(username, password));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WithBothFieldsPresent_IsValid()
    {
        var result = _validator.Validate(new LoginRequest("cashier1", "P@ssw0rd"));

        Assert.True(result.IsValid);
    }
}

public sealed class CreateProductRequestValidatorTests
{
    private readonly CreateProductRequestValidator _validator = new();

    [Fact]
    public void Validate_WithZeroPrice_IsInvalid()
    {
        var result = _validator.Validate(
            new CreateProductRequest("منتج", null, null, 0m, null, false, 0));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WithNegativeStock_IsInvalid()
    {
        var result = _validator.Validate(
            new CreateProductRequest("منتج", null, null, 10m, null, true, -1));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WithMissingNameAr_IsInvalid()
    {
        var result = _validator.Validate(
            new CreateProductRequest("", null, null, 10m, null, false, 0));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WithValidRequest_IsValid()
    {
        var result = _validator.Validate(
            new CreateProductRequest("منتج", null, null, 10m, null, true, 5));

        Assert.True(result.IsValid);
    }
}

public sealed class OpenShiftRequestValidatorTests
{
    private readonly OpenShiftRequestValidator _validator = new();

    [Fact]
    public void Validate_WithNegativeOpeningCash_IsInvalid()
    {
        var result = _validator.Validate(new OpenShiftRequest(-1));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WithZeroOpeningCash_IsValid()
    {
        var result = _validator.Validate(new OpenShiftRequest(0));

        Assert.True(result.IsValid);
    }
}
