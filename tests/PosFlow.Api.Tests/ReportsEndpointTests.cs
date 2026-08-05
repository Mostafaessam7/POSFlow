using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PosFlow.Api.Tests.TestHelpers;
using PosFlow.Application.Orders;
using PosFlow.Application.Products;
using PosFlow.Application.Reports;
using PosFlow.Application.Shifts;
using PosFlow.Domain.Entities;
using Xunit;

namespace PosFlow.Api.Tests;

public sealed class ReportsEndpointTests : IDisposable
{
    private readonly PosFlowApiFactory _factory = new();
    private readonly HttpClient _client;

    public ReportsEndpointTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task DailySummary_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/reports/daily-summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DailySummary_AfterASale_ReflectsThatSale()
    {
        var token = await AuthHelper.LoginAsAdminAsync(_client);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var productResponse = await _client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest("شاي", null, null, 25m, null, false, 0));

        var product = await productResponse.Content
            .ReadFromJsonAsync<ProductResponse>();

        await _client.PostAsJsonAsync(
            "/api/shifts/open",
            new OpenShiftRequest(0m));

        await _client.PostAsJsonAsync(
            "/api/orders/checkout",
            new CreateOrderRequest(
                Lines: [new OrderLineRequest(product!.Id, 2, 0)],
                Payments: [new PaymentRequest(PaymentMethod.Cash, 50, null)]));

        var summaryResponse = await _client.GetAsync(
            "/api/reports/daily-summary");

        Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);

        var summary = await summaryResponse.Content
            .ReadFromJsonAsync<DailySummaryResponse>();

        Assert.Equal(1, summary!.OrderCount);
        Assert.Equal(50m, summary.TotalSales);
        Assert.Equal(50m, summary.CashSales);
        Assert.Equal(0m, summary.CardSales);

        Assert.Contains(
            summary.TopProducts,
            p => p.ProductName == "شاي" && p.QuantitySold == 2);
    }
}
