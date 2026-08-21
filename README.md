# Product Management API

Backend for the product management assessment — see
`docs/superpowers/specs/2026-08-20-product-management-api-design.md` for
the full design rationale.

## Run the full stack (one command)

    docker compose up --build

Swagger UI: http://localhost:8080/swagger
API base: http://localhost:8080/api/v1

The database auto-migrates and seeds sample data on first boot (non-Production
only). To reset to a clean slate: `docker compose down -v` then `up` again.

## Active backend development (hot reload)

    docker compose up postgres redis
    cd backend/src/ProductManagement.Api
    dotnet watch run

## Running tests

    cd backend
    dotnet test tests/ProductManagement.UnitTests           # no dependencies needed
    dotnet test tests/ProductManagement.ArchitectureTests    # no dependencies needed
    docker compose up -d postgres redis                       # required first:
    dotnet test tests/ProductManagement.IntegrationTests

## Environment variables

| Variable | Default | Purpose |
|---|---|---|
| `ConnectionStrings__Default` | `Host=localhost;Port=5432;...` | Postgres connection |
| `Redis__ConnectionString` | `localhost:6379` | Redis connection |
| `Cors__AllowedOrigins__0` | `http://localhost:5173` | Front-end origin allowed by CORS |
| `Seeding__CategoryCount` | `40` | Seed data volume |
| `Seeding__ProductCount` | `5000` | Seed data volume |
| `Seeding__MaxVariantsPerProduct` | `4` | Seed data volume |

## Postman

Import `postman/ProductManagement.postman_collection.json` and
`postman/ProductManagement.postman_environment.json`.

## Known limitations

No authentication (all endpoints public — see spec §10/§11), local-disk
image storage (not real blob storage), no CI/CD pipeline. Full list in the
design spec's Limitations section.

## Front-end

See `docs/superpowers/specs/2026-08-21-product-management-frontend-design.md`
for the full design rationale.

### Active front-end development (hot reload)

    docker compose up postgres redis api    # web deliberately NOT started - avoids a port clash
    cd frontend
    npm install
    npm run dev

Open http://localhost:5173 (Vite's dev server).

### Running front-end tests

    cd frontend
    npm test

Needs nothing running — MSW mocks every API call.

### Front-end environment variables

| Variable | Default | Purpose |
|---|---|---|
| `VITE_API_BASE_URL` | `http://localhost:8080/api/v1` | Backend API base URL (browser-reachable, not the Docker-internal hostname) |

### Front-end known limitations

No login UI (mirrors the backend's no-auth decision), no real-time updates
(another user's stock change isn't reflected until refetch), no E2E test
suite (unit/component coverage only), no i18n, default antd theme.
