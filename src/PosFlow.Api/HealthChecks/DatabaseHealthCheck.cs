using Microsoft.Extensions.Diagnostics.HealthChecks;
using PosFlow.Infrastructure.Persistence;

namespace PosFlow.Api.HealthChecks;

public sealed class DatabaseHealthCheck(
    PosFlowDbContext dbContext)
    : IHealthCheck
{
    private readonly PosFlowDbContext _dbContext = dbContext;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database
                .CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy(
                    "تعذر الاتصال بقاعدة البيانات.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "فشل فحص قاعدة البيانات.",
                exception);
        }
    }
}
