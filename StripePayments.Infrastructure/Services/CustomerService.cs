using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Stripe;
using StripePayments.Application.DTOs;
using StripePayments.Application.Services;
using StripePayments.Infrastructure.Persistence;

namespace StripePayments.Infrastructure.Services;

public class CustomerService : ICustomerService
{
    private readonly AppDbContext _db;
    private readonly Stripe.CustomerService _stripeCustomers;

    public CustomerService(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];
        _stripeCustomers = new Stripe.CustomerService();
    }

    public async Task<CustomerDto> CreateCustomerAsync(CreateCustomerRequest request)
    {
        var stripeCustomer = await _stripeCustomers.CreateAsync(new CustomerCreateOptions
        {
            Email = request.Email,
            Name = request.Name
        });

        var customer = new Domain.Entities.Customer
        {
            StripeCustomerId = stripeCustomer.Id,
            Email = request.Email,
            Name = request.Name
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        return MapToDto(customer);
    }

    public async Task<CustomerDto> GetCustomerAsync(Guid id)
    {
        var customer = await _db.Customers
            .Include(c => c.Subscription)
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new KeyNotFoundException($"Customer {id} not found.");

        return MapToDto(customer);
    }

    private static CustomerDto MapToDto(Domain.Entities.Customer c) => new(
        c.Id,
        c.StripeCustomerId,
        c.Email,
        c.Name,
        c.CreatedAt,
        c.Subscription is null ? null : new SubscriptionDto(
            c.Subscription.Id,
            c.Subscription.CustomerId,
            c.Subscription.StripeSubscriptionId,
            c.Subscription.Plan,
            c.Subscription.Status,
            c.Subscription.CurrentPeriodStart,
            c.Subscription.CurrentPeriodEnd,
            c.Subscription.UpdatedAt
        )
    );
}
