# Stripe Payments API

API demonstrating third-party payment integration, webhook handling, and subscription lifecycle management using **.NET 10** and the **Stripe API**.

**Live API docs:** https://stripe-payments-api.onrender.com/scalar/v1

---

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | .NET 10 Web API |
| Payments | Stripe.NET SDK |
| Database | PostgreSQL (Neon) |
| ORM | Entity Framework Core 10 + Npgsql |
| Auth | JWT Bearer |
| Docs | Scalar (OpenAPI) |
| Deployment | Docker + Render |

---

## Project Structure

```
stripe-payments-api/
├── Dockerfile
├── StripePayments.sln
│
├── StripePayments.Domain/              # Entities and enums — no dependencies
│   ├── Entities/
│   │   ├── Customer.cs
│   │   ├── Subscription.cs
│   │   └── WebhookEvent.cs
│   └── Enums/
│       ├── SubscriptionPlan.cs         # Basic | Pro
│       └── SubscriptionStatus.cs      # Active | PastDue | Cancelled | Incomplete
│
├── StripePayments.Application/         # Interfaces and DTOs
│   ├── DTOs/
│   │   ├── CustomerDtos.cs
│   │   └── SubscriptionDtos.cs
│   └── Services/
│       ├── ICustomerService.cs
│       └── ISubscriptionService.cs
│
├── StripePayments.Infrastructure/      # EF Core, Stripe service implementations
│   ├── Persistence/
│   │   ├── AppDbContext.cs
│   │   └── Migrations/
│   ├── Services/
│   │   ├── CustomerService.cs
│   │   ├── SubscriptionService.cs
│   │   └── WebhookService.cs
│   └── DependencyInjection.cs
│
└── StripePayments.API/                 # Controllers, Program.cs
    ├── Controllers/
    │   ├── AuthController.cs
    │   ├── CustomersController.cs
    │   ├── SubscriptionsController.cs
    │   └── WebhooksController.cs
    ├── Program.cs
    └── appsettings.json
```

---

## Dependency Graph

```
Domain
  ↑
Application
  ↑
Infrastructure  ←  (Stripe SDK, EF Core, PostgreSQL)
  ↑
API             ←  (JWT, Scalar, Controllers)
```

No circular dependencies. Infrastructure implements the interfaces defined in Application.

---

## Database Schema

```
Customer
├── Id                  uuid (PK)
├── StripeCustomerId    string (unique)
├── Email               string
├── Name                string
└── CreatedAt           timestamp

Subscription
├── Id                  uuid (PK)
├── CustomerId          uuid (FK → Customer, unique)
├── StripeSubscriptionId string (unique)
├── StripePriceId       string
├── Plan                enum  (Basic = 0 | Pro = 1)
├── Status              enum  (Active | PastDue | Cancelled | Incomplete)
├── CurrentPeriodStart  timestamp
├── CurrentPeriodEnd    timestamp
└── UpdatedAt           timestamp

WebhookEvent
├── Id                  uuid (PK)
├── StripeEventId       string (unique — idempotency key)
├── EventType           string
├── ProcessedAt         timestamp
└── Payload             text
```

---

## API Endpoints

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/auth/token` | None | Issue a JWT for testing |
| POST | `/api/customers` | JWT | Create customer in Stripe + DB |
| GET | `/api/customers/{id}` | JWT | Get customer with subscription |
| POST | `/api/subscriptions` | JWT | Create subscription in Stripe + DB |
| GET | `/api/subscriptions/{customerId}` | JWT | Get subscription status |
| POST | `/api/subscriptions/{id}/cancel` | JWT | Cancel subscription |
| POST | `/api/webhooks/stripe` | None | Receive Stripe webhook events |

---

## Webhook Events Handled

| Event | Action |
|-------|--------|
| `invoice.paid` | Set subscription status → **Active** |
| `invoice.payment_failed` | Set subscription status → **PastDue** |
| `customer.subscription.deleted` | Set subscription status → **Cancelled** |

All other event types are logged and acknowledged with 200.

---

## Design Decisions

### Webhook idempotency
Stripe can deliver the same event more than once. Before processing any event, the `StripeEventId` is checked against the `WebhookEvent` table. Duplicate events are silently skipped, making the endpoint safe to retry.

### Always return 200 to Stripe
The webhook endpoint returns 200 for all handled and unhandled events. The only exception is an invalid signature (400), which tells Stripe the payload was tampered with and should not be retried. Any other error returns 200 to prevent Stripe from flooding the endpoint with retries.

### Signature verification
Every webhook request is verified using `EventUtility.ConstructEvent` with the Stripe webhook signing secret. Requests without a valid `Stripe-Signature` header are rejected immediately.

### Subscription status in our DB
Subscription state is stored locally and kept in sync via webhooks rather than queried from Stripe on every request. This makes reads fast and resilient to Stripe API outages.

---

## Local Setup

**Prerequisites:** .NET 10 SDK, PostgreSQL

```bash
# Clone
git clone https://github.com/pixelm123/stripe-payments-api.git
cd stripe-payments-api

# Set user secrets
cd StripePayments.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=stripe_payments;Username=...;Password=..."
dotnet user-secrets set "Jwt:Key" "your-secret-key-at-least-32-characters"
dotnet user-secrets set "Jwt:Issuer" "stripe-payments-api"
dotnet user-secrets set "Jwt:Audience" "stripe-payments-client"
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."
dotnet user-secrets set "Stripe:BasicPriceId" "price_..."
dotnet user-secrets set "Stripe:ProPriceId" "price_..."

# Run migrations
cd ..
dotnet ef database update --project StripePayments.Infrastructure --startup-project StripePayments.API

# Run
$env:ASPNETCORE_ENVIRONMENT="Development"; dotnet run --project StripePayments.API
```

Scalar API docs: `http://localhost:5000/scalar/v1`

---

## Webhook Testing (Stripe CLI)

```bash
stripe listen --forward-to localhost:5000/api/webhooks/stripe
```

Copy the `whsec_...` secret printed by the CLI into your user secrets as `Stripe:WebhookSecret`.

---

## Deployment (Render)

The included `Dockerfile` builds and serves the API on port 8080.

Set the following environment variables in the Render dashboard:

```
ASPNETCORE_ENVIRONMENT              Production
ConnectionStrings__DefaultConnection Host=...;Database=...;Username=...;Password=...;SslMode=Require
Stripe__SecretKey                   sk_test_...
Stripe__WebhookSecret               whsec_...
Stripe__BasicPriceId                price_...
Stripe__ProPriceId                  price_...
Jwt__Key                            <random 32+ char string>
Jwt__Issuer                         stripe-payments-api
Jwt__Audience                       stripe-payments-client
```

After first deploy, run migrations against your Neon database:

```powershell
$env:ConnectionStrings__DefaultConnection="Host=...neon...;..."
dotnet ef database update --project StripePayments.Infrastructure --startup-project StripePayments.API
```
