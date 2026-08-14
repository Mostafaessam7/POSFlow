using Microsoft.EntityFrameworkCore;
using PosFlow.Application.Common;
using PosFlow.Application.ExchangeRates;
using PosFlow.Domain.Entities;
using PosFlow.Infrastructure.Persistence;

namespace PosFlow.Infrastructure.ExchangeRates;

public sealed class ExchangeRateService(
    PosFlowDbContext dbContext,
    ICurrentUser currentUser)
    : IExchangeRateService
{
    private readonly PosFlowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<IReadOnlyList<ExchangeRateResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var rates = await _dbContext.ExchangeRates
            .AsNoTracking()
            .Where(x => x.TenantId == _currentUser.TenantId)
            .OrderBy(x => x.CurrencyCode)
            .ToListAsync(cancellationToken);

        return rates
            .Select(MapResponse)
            .ToList();
    }

    public async Task<ExchangeRateResponse> UpsertAsync(
        UpsertExchangeRateRequest request,
        CancellationToken cancellationToken = default)
    {
        var currencyCode = request.CurrencyCode.Trim().ToUpperInvariant();

        var rate = await _dbContext.ExchangeRates
            .SingleOrDefaultAsync(
                x =>
                    x.TenantId == _currentUser.TenantId &&
                    x.CurrencyCode == currencyCode,
                cancellationToken);

        if (rate is null)
        {
            rate = new ExchangeRate
            {
                TenantId = _currentUser.TenantId,
                CurrencyCode = currencyCode,
                RatePerBaseUnit = request.RatePerBaseUnit
            };

            _dbContext.ExchangeRates.Add(rate);
        }
        else
        {
            rate.RatePerBaseUnit = request.RatePerBaseUnit;
            rate.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapResponse(rate);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var rate = await _dbContext.ExchangeRates
            .SingleOrDefaultAsync(
                x =>
                    x.Id == id &&
                    x.TenantId == _currentUser.TenantId,
                cancellationToken);

        if (rate is null)
        {
            throw new KeyNotFoundException("سعر الصرف غير موجود.");
        }

        _dbContext.ExchangeRates.Remove(rate);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ConvertAmountResponse> ConvertAsync(
        decimal amount,
        string toCurrencyCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = toCurrencyCode.Trim().ToUpperInvariant();

        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .SingleAsync(
                x => x.Id == _currentUser.TenantId,
                cancellationToken);

        if (normalizedCode == tenant.CurrencyCode)
        {
            return new ConvertAmountResponse(
                amount,
                tenant.CurrencyCode,
                normalizedCode,
                RatePerBaseUnit: 1m,
                ConvertedAmount: amount);
        }

        var rate = await _dbContext.ExchangeRates
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.TenantId == _currentUser.TenantId &&
                    x.CurrencyCode == normalizedCode,
                cancellationToken);

        if (rate is null)
        {
            throw new KeyNotFoundException(
                $"لا يوجد سعر صرف مضبوط لعملة {normalizedCode}.");
        }

        var converted = Math.Round(
            amount * rate.RatePerBaseUnit,
            2,
            MidpointRounding.AwayFromZero);

        return new ConvertAmountResponse(
            amount,
            tenant.CurrencyCode,
            normalizedCode,
            rate.RatePerBaseUnit,
            converted);
    }

    private static ExchangeRateResponse MapResponse(
        ExchangeRate rate)
    {
        return new ExchangeRateResponse(
            Id: rate.Id,
            CurrencyCode: rate.CurrencyCode,
            RatePerBaseUnit: rate.RatePerBaseUnit,
            UpdatedAtUtc: rate.UpdatedAtUtc);
    }
}
