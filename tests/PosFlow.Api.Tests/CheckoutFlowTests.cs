using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PosFlow.Api.Tests.TestHelpers;
using PosFlow.Application.Orders;
using PosFlow.Application.Products;
using PosFlow.Application.Shifts;
using PosFlow.Domain.Entities;
using Xunit;

namespace PosFlow.Api.Tests;

public sealed class CheckoutFlowTests : IDisposable
{
    private readonly PosFlowApiFactory _factory = new();
    private readonly HttpClient _client;

    public CheckoutFlowTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Checkout_WithoutOpenShift_ReturnsConflictViaGlobalExceptionHandler()
    {
        await AuthenticateAsAdminAsync();

        var product = await CreateProductAsync("قلم رصاص", 5m);

        var checkoutResponse = await _client.PostAsJsonAsync(
            "/api/orders/checkout",
            new CreateOrderRequest(
                Lines: [new OrderLineRequest(product.Id, 1, 0)],
                Payments: [new PaymentRequest(PaymentMethod.Cash, 5, null)]));

        // No try/catch anywhere in OrdersController for this - it's
        // GlobalExceptionHandler turning the service's
        // InvalidOperationException into a 409 with a JSON body.
        Assert.Equal(HttpStatusCode.Conflict, checkoutResponse.StatusCode);

        var body = await checkoutResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.True(body.TryGetProperty("message", out var message));
        Assert.False(string.IsNullOrWhiteSpace(message.GetString()));
    }

    [Fact]
    public async Task FullSale_OpenShiftThenCheckoutThenCloseShift_ReconcilesCashCorrectly()
    {
        await AuthenticateAsAdminAsync();

        var product = await CreateProductAsync("كوب قهوة", 20m);

        var openShiftResponse = await _client.PostAsJsonAsync(
            "/api/shifts/open",
            new OpenShiftRequest(100m));

        openShiftResponse.EnsureSuccessStatusCode();

        var shift = await openShiftResponse.Content
            .ReadFromJsonAsync<ShiftResponse>();

        var checkoutResponse = await _client.PostAsJsonAsync(
            "/api/orders/checkout",
            new CreateOrderRequest(
                Lines: [new OrderLineRequest(product.Id, 2, 0)],
                Payments: [new PaymentRequest(PaymentMethod.Cash, 40, null)]));

        Assert.Equal(HttpStatusCode.Created, checkoutResponse.StatusCode);

        var order = await checkoutResponse.Content
            .ReadFromJsonAsync<OrderResponse>();

        Assert.Equal("Completed", order!.Status);
        Assert.Equal(40m, order.TotalAmount);
        Assert.Equal(0m, order.ChangeDue);

        var closeResponse = await _client.PostAsJsonAsync(
            $"/api/shifts/{shift!.Id}/close",
            new CloseShiftRequest(140m));

        closeResponse.EnsureSuccessStatusCode();

        var closedShift = await closeResponse.Content
            .ReadFromJsonAsync<ShiftResponse>();

        Assert.Equal("Closed", closedShift!.Status);
        Assert.Equal(40m, closedShift.CashSales);
        Assert.Equal(140m, closedShift.ExpectedCash); // 100 opening + 40 cash
        Assert.Equal(0m, closedShift.CashDifference);
    }

    [Fact]
    public async Task Checkout_WithStockTrackedProduct_RejectsOversell()
    {
        await AuthenticateAsAdminAsync();

        var createResponse = await _client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest(
                "منتج محدود", null, null, 15m, null, TrackStock: true, StockQuantity: 1));

        createResponse.EnsureSuccessStatusCode();
        var product = await createResponse.Content
            .ReadFromJsonAsync<ProductResponse>();

        await _client.PostAsJsonAsync(
            "/api/shifts/open",
            new OpenShiftRequest(0m));

        var checkoutResponse = await _client.PostAsJsonAsync(
            "/api/orders/checkout",
            new CreateOrderRequest(
                Lines: [new OrderLineRequest(product!.Id, 5, 0)],
                Payments: [new PaymentRequest(PaymentMethod.Cash, 75, null)]));

        Assert.Equal(HttpStatusCode.Conflict, checkoutResponse.StatusCode);
    }

    private async Task AuthenticateAsAdminAsync()
    {
        var token = await AuthHelper.LoginAsAdminAsync(_client);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<ProductResponse> CreateProductAsync(
        string nameAr,
        decimal price)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest(nameAr, null, null, price, null, false, 0));

        response.EnsureSuccessStatusCode();

        return (await response.Content
            .ReadFromJsonAsync<ProductResponse>())!;
    }
}
