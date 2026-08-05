using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PosFlow.Api.Tests.TestHelpers;
using PosFlow.Application.Categories;
using PosFlow.Application.Products;
using Xunit;

namespace PosFlow.Api.Tests;

public sealed class CategoriesEndpointTests : IDisposable
{
    private readonly PosFlowApiFactory _factory = new();
    private readonly HttpClient _client;

    public CategoriesEndpointTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task AuthenticateAsAdminAsync()
    {
        var token = await AuthHelper.LoginAsAdminAsync(_client);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task GetCategories_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateThenListCategories_AsAdmin_Succeeds()
    {
        await AuthenticateAsAdminAsync();

        var createResponse = await _client.PostAsJsonAsync(
            "/api/categories",
            new CreateCategoryRequest("مشروبات", "Drinks"));

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var listResponse = await _client.GetAsync("/api/categories");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var categories = await listResponse.Content
            .ReadFromJsonAsync<List<CategoryResponse>>();

        Assert.Contains(categories!, c => c.NameAr == "مشروبات");
    }

    [Fact]
    public async Task DeleteCategory_WithProductsAttached_ReturnsConflict()
    {
        await AuthenticateAsAdminAsync();

        var createCategoryResponse = await _client.PostAsJsonAsync(
            "/api/categories",
            new CreateCategoryRequest("أدوات", null));

        var category = await createCategoryResponse.Content
            .ReadFromJsonAsync<CategoryResponse>();

        await _client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest(
                "منتج", null, null, 10m, category!.Id, false, 0));

        var deleteResponse = await _client.DeleteAsync(
            $"/api/categories/{category.Id}");

        Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_WithoutProducts_Succeeds()
    {
        await AuthenticateAsAdminAsync();

        var createCategoryResponse = await _client.PostAsJsonAsync(
            "/api/categories",
            new CreateCategoryRequest("فارغ", null));

        var category = await createCategoryResponse.Content
            .ReadFromJsonAsync<CategoryResponse>();

        var deleteResponse = await _client.DeleteAsync(
            $"/api/categories/{category!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }
}
