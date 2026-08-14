using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosFlow.Application.Common;
using PosFlow.Application.ExchangeRates;

namespace PosFlow.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/exchange-rates")]
public sealed class ExchangeRatesController(
    IExchangeRateService exchangeRateService)
    : ControllerBase
{
    private readonly IExchangeRateService _exchangeRateService = exchangeRateService;

    /// <summary>Open to any authenticated user - a cashier needs these rates to show a converted total on the receipt.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExchangeRateResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var rates = await _exchangeRateService.GetAllAsync(cancellationToken);

        return Ok(rates);
    }

    [HttpGet("convert")]
    public async Task<ActionResult<ConvertAmountResponse>> Convert(
        [FromQuery] decimal amount,
        [FromQuery] string to,
        CancellationToken cancellationToken)
    {
        var result = await _exchangeRateService.ConvertAsync(
            amount,
            to,
            cancellationToken);

        return Ok(result);
    }

    [Authorize(Policy = Permissions.TenantSettingsManage)]
    [HttpPut]
    public async Task<ActionResult<ExchangeRateResponse>> Upsert(
        UpsertExchangeRateRequest request,
        CancellationToken cancellationToken)
    {
        var rate = await _exchangeRateService.UpsertAsync(
            request,
            cancellationToken);

        return Ok(rate);
    }

    [Authorize(Policy = Permissions.TenantSettingsManage)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _exchangeRateService.DeleteAsync(id, cancellationToken);

        return NoContent();
    }
}
