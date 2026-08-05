using PosFlow.Application.Common;

namespace PosFlow.Application.Customers;

public interface ICustomerService
{
    Task<PagedResult<CustomerResponse>> GetAllAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<CustomerResponse> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerResponse> UpdateAsync(
        Guid id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default);
}
