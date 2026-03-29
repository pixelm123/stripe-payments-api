using StripePayments.Application.DTOs;

namespace StripePayments.Application.Services;

public interface ICustomerService
{
    Task<CustomerDto> CreateCustomerAsync(CreateCustomerRequest request);
    Task<CustomerDto> GetCustomerAsync(Guid id);
}
