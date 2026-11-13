# Stitchling — a custom plushie shop

An online shop for selling plushies: ready-made ones with colour/size variants, and one-of-a-kind
plushies customers design themselves in a live builder. Orders are shipped through
[Chit Chats](https://chitchats.com/docs/api/v1).

| Part | Tech | Where |
|---|---|---|
| API | C# / ASP.NET Core 10, EF Core, JWT auth | `backend/src/Plushies.Api` |
| Database | PostgreSQL 16 | migrations in `backend/src/Plushies.Api/Data/Migrations` |
| Storefront + admin | React 19, TypeScript, Vite, SCSS | `frontend/` |
| Tests | xUnit, against real PostgreSQL | `backend/tests/Plushies.Tests` |
| Chit Chats simulator | small ASP.NET app for local dev and tests | `backend/tools/ChitChatsSimulator` |

## Run it locally

You need the **.NET 10 SDK**, **Node 22+** and **PostgreSQL 16**.

1. Create the database (once):

   ```bash
   psql -U postgres -c "CREATE ROLE plushies LOGIN PASSWORD 'plushies' CREATEDB;" -c "CREATE DATABASE plushies OWNER plushies;"
   ```

2. Start the API on http://localhost:5080. On first start it creates the tables and loads demo data.

   ```bash
   dotnet run --project backend/src/Plushies.Api
   ```

3. Start the storefront on http://localhost:5173 (it proxies `/api` to the API).

   ```bash
   cd frontend && npm install && npm run dev
   ```

Development sign-ins (from `appsettings.Development.json` and the demo seed — change them before going live):

| Who | Where | Email | Password |
|---|---|---|---|
| Customer | http://localhost:5173/login | `demo@stitchling.test` | `plushie-demo-123` |
| Admin | http://localhost:5173/admin | `admin@stitchling.test` | `stitch-admin-123` |

Try the coupons `WELCOME10` (10% off) and `FREEHUG` ($5 off orders over $50).

### Or with Docker

```bash
cp .env.example .env    # then edit the values
```

```bash
docker compose up --build
```

The shop is then on http://localhost:8080 and the admin site on http://localhost:8080/admin.
One container serves both the API and the built React app.

## Shipping with Chit Chats

The integration follows the [Chit Chats API v1](https://chitchats.com/docs/api/v1). In the admin
site, open an order and use the **Shipping · Chit Chats** panel:

1. **Create shipment & get rates** — `POST /shipments` with `postage_type: "unknown"`. The order's
   address, declared value and one customs `line_items` entry per order item are sent.
2. **Buy postage** — `PATCH /shipments/{id}/buy`, then the API polls until the shipment leaves
   `postage_requested` (as the docs describe).
3. **Download label** — PDF, PNG or ZPL, fetched server-side so the access token never reaches the browser.
4. **Sync** — pulls the latest status and tracking. A background job also does this every
   30 minutes (`ChitChats:SyncIntervalMinutes`). When Chit Chats reports the parcel received /
   in transit the order becomes *shipped*; when delivered, *delivered*. Customers see the tracking
   link on their order page.
5. **Refund postage / delete shipment** — `PATCH /shipments/{id}/refund` and `DELETE /shipments/{id}`.

Connect your account by setting these on the API (environment variables, `.env` for Docker, or
`dotnet user-secrets`):

| Setting | Value |
|---|---|
| `ChitChats__ClientId` | your client ID |
| `ChitChats__AccessToken` | from Chit Chats → Settings → Developer → API Access Tokens |
| `ChitChats__BaseUrl` | `https://staging.chitchats.com/api/v1` (sandbox, the default) or `https://chitchats.com/api/v1` (live) |
| `ChitChats__DefaultHsTariffCode` | HS code for products that don't set their own — confirm it with your customs broker |

Other options (default parcel size, packaging weight, item description) are in the `ChitChats`
section of `appsettings.json`.

**Why checkout doesn't show live Chit Chats rates:** their API only returns rates for a shipment
that already exists, so the storefront charges flat rates from the `shipping_method` table
(editable under Admin → Shipping) and you buy the real postage when you pack the order.

### Trying the shipping flow without a Chit Chats account

```bash
dotnet run --project backend/tools/ChitChatsSimulator
```

```bash
ChitChats__BaseUrl=http://localhost:5090/api/v1 ChitChats__ClientId=123456 ChitChats__AccessToken=sim-token dotnet run --project backend/src/Plushies.Api
```

The simulator checks the same required fields and allowed values as the published spec, but its
rates, tracking numbers and labels are made up. `POST http://localhost:5090/_sim/shipments/{id}/advance`
moves a shipment to its next status so you can watch an order go to shipped and delivered.
**It is not a substitute for a run against the real sandbox before going live.**

## Payments

Checkout goes through `IPaymentGateway` (`backend/src/Plushies.Api/Features/Orders/Payments.cs`).
The only implementation included is a **test gateway that charges nothing** — the checkout page
says so and lets you pick an approved or a declined test card. To take real money, implement
`IPaymentGateway` for your processor (Stripe, Square, …), register it in `Program.cs`, and replace
the test-card panel in `frontend/src/pages/Checkout.tsx` with the processor's card form.
The API refuses to start with the test gateway outside Development unless
`Payments__AllowTestGateway=true`.

## Tests

```bash
dotnet test backend
```

The tests start the real API against a throwaway PostgreSQL database and the Chit Chats simulator.
They need a local PostgreSQL where the `plushies` role may create databases (step 1 above), or
set `TEST_POSTGRES` to another server's connection string.

Frontend checks:

```bash
cd frontend && npm run build && npm run lint
```

## Database

The schema completes the original ER diagram. `docs/database.md` lists every table and explains
what was added and the few things that were changed, and why.

After changing an entity in `backend/src/Plushies.Api/Domain`, add a migration:

```bash
cd backend && dotnet tool restore && dotnet ef migrations add DescribeTheChange --project src/Plushies.Api -o Data/Migrations
```

Migrations are applied when the API starts (`Database:MigrateOnStartup`).

## Before going live

- Set `Jwt__Key` to a long random secret, and real `Seed__AdminEmail` / `Seed__AdminPassword`.
- Leave `Seed__DemoData` off so the demo customers and catalogue are not created.
- Add a real payment gateway (see above).
- Point `ChitChats__BaseUrl` at the live API and test one real shipment in the sandbox first.
- Review the seeded tax rates (Admin → Tax) with your accountant.
- Serve over HTTPS. If the API sits behind a reverse proxy, forward the client IP, because the
  sign-in rate limit is per client address.
- Order confirmation emails are not sent yet; customers follow orders from their account page.
