# .NET Modular Monolith Template

A .NET 11 starter for a modular monolith. Group use cases into vertical slices and deploy the backend as a single application. The sample Sales module creates orders through Wolverine HTTP and persists them with EF Core and PostgreSQL.

## Quick start

Choose **Use this template** on GitHub and clone your new repository. With Docker Compose and Make installed, run from the repository root.

```sh
cp .env.example .env
make up-all
```

Open the [React frontend](http://127.0.0.1:3000) or explore the API in [Scalar](http://127.0.0.1:8080/scalar). The OpenAPI document is at `http://127.0.0.1:8080/openapi/v1.json`. Set the host ports in `.env`, and use `make down` to stop the services and keep the database volume.

Create an order to see the first slice in action.

```sh
curl -i http://127.0.0.1:8080/api/v1/orders \
  -H 'Content-Type: application/json' \
  -d '{"orderLines":[{"productId":7,"quantity":3}]}'
```

## Architecture

- **Modular monolith and vertical slices.** Organize features by module, with each use case owning its request, handler, HTTP endpoint, and outcomes. The Sales example uses an order aggregate and value objects. [CreateOrder slice](src/backend/src/Api/Modules/Sales/Features/Orders/CreateOrderRepr)
- **Wolverine HTTP and CQRS.** Run command handlers with an EF Core transaction. Synchronous domain reactions share the active `DbContext` and can raise further events. Integration events use the durable PostgreSQL outbox for delivery after commit. [Backend guide](src/backend/README.md)
- **Result pattern with C# 15 unions.** The order handler returns `Outcome<OrderCreated, CreateOrderError>`. The slice declares its success and error cases in a dedicated file. The endpoint handles each case and returns typed JSON with a stable error code. The compiler checks that the switch covers every case. [CreateOrderOutcomes.cs](src/backend/src/Api/Modules/Sales/Features/Orders/CreateOrderRepr/CreateOrderOutcomes.cs)
- **React starter.** Build the UI with React 19, Vite, and TypeScript. The starter includes routing, React Query, and example component tests. [Frontend source](src/client)

## Tests

Run the .NET suite with the .NET 11 SDK and Docker.

```sh
dotnet test src/backend/BackendTemplate.slnx
```

Architecture tests check that domain types stay independent of the application and infrastructure layers and that modules have no dependency cycles. PostgreSQL integration tests cover nested domain events, rollback, and manual outbox writes.

With Node.js 24, run the frontend tests or start the full stack in an isolated Docker environment for Playwright.

```sh
npm --prefix src/client ci
npm --prefix src/client test -- --run
make e2e-local-all
```

Playwright checks the OpenAPI schemas, order creation, typed error responses, and browser access to the API. Repositories created from this template include a [GitHub Actions workflow](.github/workflows/fullstack-ci.yml) for the .NET and Playwright suites.
