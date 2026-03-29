using Microsoft.EntityFrameworkCore;
using StripePayments.Domain.Entities;

namespace StripePayments.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StripeCustomerId).IsRequired();
            entity.Property(e => e.Email).IsRequired();
            entity.Property(e => e.Name).IsRequired();
            entity.HasIndex(e => e.StripeCustomerId).IsUnique();
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StripeSubscriptionId).IsRequired();
            entity.Property(e => e.StripePriceId).IsRequired();
            entity.HasIndex(e => e.StripeSubscriptionId).IsUnique();
            entity.HasOne(e => e.Customer)
                .WithOne(c => c.Subscription)
                .HasForeignKey<Subscription>(e => e.CustomerId);
        });

        modelBuilder.Entity<WebhookEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StripeEventId).IsRequired();
            entity.HasIndex(e => e.StripeEventId).IsUnique();
            entity.Property(e => e.Payload).IsRequired();
        });
    }
}
