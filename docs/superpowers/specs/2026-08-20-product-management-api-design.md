# Product Management API — Design Spec

Date: 2026-08-20
Status: Approved for implementation

## 1. Context & Goal

Build product management endpoints for a retail/e-commerce application (fashion shop
example), for a take-home assessment. Evaluation criteria: development approach,
database design (SQL vs NoSQL rationale, extensibility), tech stack (ORM, validation),
API/data handling, and performance (caching, concurrency).

**Scope**: core product catalog management — products, categories, variants
(size/color/SKU/price/stock), with concurrency-safe stock updates. Explicitly
**out of scope**: cart, checkout, orders, payments, and product images (removed from
scope during design review — image handling adds no new insight into the
consistency/scalability story this assessment is testing).

**Depth target**: take-home realistic scope — fully working API, DB, and tests for
the in-scope features; auth is a minimal stand-in (single API key), not a full
identity system; no CI/CD pipeline. Both are documented as future improvements
rather than built.

## 2. Development Approach

1. Design the schema and consistency strategy first (this doc) — the hardest
   decisions (SQL vs NoSQL, how stock avoids overselling) shape everything else.
2. Scaffold the ASP.NET Core solution, EF Core model, and Postgres/Redis
   docker-compose environment.
3. Implement endpoints bottom-up: categories → products → variants → stock,
   each with validation, error handling, and tests before moving to the next.
4. Add caching once the read endpoints exist and are correct — caching is a
   performance layer on top of correct behavior, not a substitute for it.
5. Write integration tests for the concurrency-critical path (parallel stock
   decrements) using a real ephemeral Postgres (Testcontainers), not mocks —
   consistency guarantees are meaningless if only tested against an in-memory
   fake.
6. Produce the Postman collection and design/limitations documentation
   alongside the code, not after — endpoints and docs should never drift.

## 3. Database Design

### 3.1 SQL vs NoSQL

**Choice: PostgreSQL.** Product catalogs are strongly relational (product →
variants → category hierarchy, FK integrity between price/stock and their
owning entities), and the assessment explicitly requires **strong
consistency** — which an ACID relational database with row-level locking
provides natively. A NoSQL store (e.g. MongoDB) would need to reimplement
transactional guarantees at the application layer to match this, and is a
better fit for read-only/denormalized-at-massive-scale catalogs, which is not
the constraint here.

### 3.2 Schema

```
categories(
  id                 bigint identity PK,
  name               varchar(120)   NOT NULL,
  slug               citext         NOT NULL UNIQUE,
  parent_category_id bigint         NULL FK -> categories(id),
  display_order      int            NOT NULL DEFAULT 0,
  is_active          boolean        NOT NULL DEFAULT true,
  created_at         timestamptz    NOT NULL DEFAULT now(),
  updated_at         timestamptz    NOT NULL DEFAULT now()
)
-- indexes: unique(slug), btree(parent_category_id),
--          partial btree(id) WHERE is_active

products(
  id          bigint identity PK,
  name        varchar(200)  NOT NULL,
  slug        citext        NOT NULL UNIQUE,
  description text          NULL,
  category_id bigint        NOT NULL FK -> categories(id),
  brand       varchar(100)  NULL,
  status      smallint      NOT NULL DEFAULT 0,  -- 0=Draft,1=Active,2=Archived
  attributes  jsonb         NOT NULL DEFAULT '{}',
  created_at  timestamptz   NOT NULL DEFAULT now(),
  updated_at  timestamptz   NOT NULL DEFAULT now()
  -- xmin (Postgres system column) used as EF Core concurrency token
)
-- indexes: unique(slug), btree(category_id), btree(status),
--          GIN(attributes jsonb_path_ops), GIN trigram(name) via pg_trgm

product_variants(
  id                bigint identity PK,
  product_id        bigint        NOT NULL FK -> products(id) ON DELETE CASCADE,
  sku               varchar(64)   NOT NULL UNIQUE,
  size              varchar(20)   NULL,
  color             varchar(40)   NULL,
  price             numeric(12,2) NOT NULL CHECK (price >= 0),
  compare_at_price  numeric(12,2) NULL
                    CHECK (compare_at_price IS NULL OR compare_at_price >= price),
  stock_quantity    int           NOT NULL DEFAULT 0 CHECK (stock_quantity >= 0),
  barcode           varchar(64)   NULL,
  is_active         boolean       NOT NULL DEFAULT true,
  created_at        timestamptz   NOT NULL DEFAULT now(),
  updated_at        timestamptz   NOT NULL DEFAULT now()
)
-- indexes: unique(sku), btree(product_id),
--          partial btree(product_id) WHERE is_active AND stock_quantity > 0
```

Relationships: `categories` 1—N `products` (required FK, single category per
product — confirmed, no many-to-many); `products` 1—N `product_variants`
(required, `ON DELETE CASCADE`). No cross-table cycles.

**Extensibility for new product attributes**: `products.attributes` is
`jsonb` with a GIN index, so new fashion-specific attributes (material, fit,
care instructions, etc.) ship with zero migrations and are still filterable
(`attributes @> '{"material":"cotton"}'`). Well-known attributes can be
promoted to first-class columns later if they need strict typing or
heavier indexing — this is the standard Postgres middle ground between a
rigid fixed schema and full schemaless NoSQL.

**Soft delete**: `products.status = Archived` and `product_variants.is_active
= false` are the delete states; no separate `deleted_at` column, and no
public hard-delete endpoint (hard delete/cascade is a DB-level behavior used
only internally, not exposed as an API operation, to avoid destructive
accidents on a live catalog).

### 3.3 Consistency Strategy

Two mechanisms, chosen per write pattern:

- **Stock changes** (the concurrency-critical path): a single atomic SQL
  statement inside a transaction —
  `UPDATE product_variants SET stock_quantity = stock_quantity - :qty
  WHERE id = :id AND stock_quantity >= :qty`. Zero rows affected means
  insufficient stock → `409 Conflict`. Postgres's row-level lock during the
  UPDATE itself prevents any interleaving that could oversell, without a
  read-then-write race window, long-held transactions, or `SERIALIZABLE`
  isolation.
- **General field edits** (name, price, category, etc.): optimistic
  concurrency via Postgres's native `xmin` system column, exposed to EF Core
  as a concurrency token. Conflicting concurrent edits return `409` with the
  current server state so the client can retry.

Both write paths surface conflicts as `409` uniformly, so API consumers need
only one conflict-handling code path regardless of which mechanism is
underneath.

## 4. Scalability

Catalog traffic is read-dominant (browsing >> catalog edits), so scalability
work concentrates on reads, while the strong-consistency work stays narrowly
scoped to the one write path that actually needs it (stock).

**Read scalability:**
- Redis cache-aside for product detail/listing/category GETs — dominant
  traffic pattern hits Redis, not Postgres.
- Keyset (cursor) pagination on `GET /products`, not `OFFSET` — stays
  index-bound (`O(log n)`) regardless of catalog size or page depth, unlike
  `OFFSET n` which forces Postgres to scan and discard `n` rows.
- Indexes matched to actual query patterns (category filter, slug lookup,
  attribute filter, name search) so query cost stays flat as row count grows.
- Stateless API layer — horizontally scaled behind a load balancer with
  nothing shared to coordinate.
- **Documented future step, not built**: Postgres read replicas (route GETs
  to replicas, writes to primary) and PgBouncer connection pooling — the
  design doesn't block this (no server-side session state, reads/writes
  already separated in code).

**Write scalability:**
- The atomic stock UPDATE takes a row-level lock, not a table lock — writes
  to different variants never contend, and the lock on a given row is held
  for microseconds (single statement, no app round-trip in between). Write
  throughput scales with distinct variants touched, not bottlenecked by the
  table.
- Deliberately no long-held transactions or `SERIALIZABLE` isolation, since
  those hurt write scalability under contention.
- Catalog-edit writes (name/price/etc.) are low-QPS (admin activity, not
  customer traffic) and use plain transactional writes — no special handling
  needed.

**Not built, documented as future improvements**: read replicas, PgBouncer,
sharding by category/seller if the catalog became multi-tenant at extreme
size.

## 5. Technology Stack

- **Framework**: ASP.NET Core Web API (.NET 10)
- **ORM**: EF Core (Npgsql provider) — code-first migrations; `xmin` as
  native concurrency token; the stock UPDATE is raw SQL via
  `ExecuteSqlInterpolatedAsync` since it must be a single hand-written atomic
  statement, not LINQ-generated.
- **Validation**: FluentValidation — one validator per request DTO. Chosen
  over DataAnnotations because several rules are cross-field (e.g.
  `compare_at_price >= price`), which DataAnnotations handles awkwardly.
- **Mapping**: manual extension methods (`ToDto()` / `ToEntity()`) — the
  entity/DTO shape difference is small enough that AutoMapper/Mapster would
  add indirection without saving real effort.
- **Auth**: single API key (`X-Api-Key` header) gating all write endpoints;
  GET endpoints are public. Explicitly a stand-in for real JWT + RBAC
  (documented future improvement).
- **Caching**: `StackExchange.Redis`, cache-aside pattern.
- **Logging**: Serilog, structured JSON to console.
- **Testing**: xUnit + Testcontainers (real ephemeral Postgres) — required
  for the concurrency test (fire N parallel stock-decrement requests, assert
  stock never goes negative and exactly the right number of requests
  succeed).
- **API docs**: Swashbuckle (OpenAPI/Swagger UI) generated from code, plus a
  separately maintained Postman collection for the submission.

## 6. API Design

Base path `/api/v1`, JSON throughout, resource-oriented REST.

### Categories
- `GET /categories` — flat list; `?parentId=`, `?activeOnly=true`
- `GET /categories/{id}`
- `POST /categories`
- `PUT /categories/{id}`
- `DELETE /categories/{id}` — `409` if active products still reference it

### Products
- `GET /products` — filters: `categoryId`, `status`, `q` (trigram search on
  name), `minPrice`/`maxPrice`, `attributes` (JSON containment match);
  cursor pagination (`?cursor=&limit=`, `limit` capped at 100); returns a
  lightweight list DTO (no variant/detail payload)
- `GET /products/{id}` / `GET /products/slug/{slug}` — full detail incl.
  variants
- `POST /products` — body may include an initial `variants[]` array,
  created transactionally with the product
- `PUT /products/{id}` — full replace; requires `If-Match` with the version
  token; `409` on mismatch
- `PATCH /products/{id}` — partial update, same concurrency check
- `DELETE /products/{id}` — soft delete (`status = Archived`)

### Variants
- `GET /products/{productId}/variants`
- `POST /products/{productId}/variants` — `409` on duplicate SKU
- `PUT /products/{productId}/variants/{variantId}`
- `DELETE /products/{productId}/variants/{variantId}` — soft delete
  (`is_active = false`)
- `PATCH /products/{productId}/variants/{variantId}/stock` — body
  `{ "delta": -3 }` (negative = decrement/sale, positive = restock);
  atomic conditional UPDATE per §3.3; optional `Idempotency-Key` header
  (checked against Redis, short TTL) so a retried request can't
  double-decrement stock

### Input/Output Handling

Every write: FluentValidation → business-rule check (FK existence,
uniqueness) → DB write inside a transaction → mapped response DTO. Errors
use **RFC 7807 `ProblemDetails`** uniformly: `400` validation, `404` not
found, `409` conflict (duplicate SKU/slug, concurrency, insufficient stock),
`422` reserved for business-rule violations not expressible as structural
validation. All responses use DTOs, never raw entities, so internal schema
changes never leak into the API contract.

## 7. Performance & Caching

- `GET /products/{id}` and `/products/slug/{slug}` → `product:{id}`, TTL 10
  min.
- `GET /products` (list) → keyed by a hash of query params
  (`products:list:{hash}`), TTL 60s.
- `GET /categories` → cached, TTL 30 min.
- **Invalidation**: writes delete the specific `product:{id}` key
  immediately (price/stock must never be served stale after a write, even
  though cached). List-cache invalidation uses a version-bump prefix
  (`products:list:v{n}:{hash}`) rather than `SCAN`/`KEYS`, avoiding
  production-unsafe cache-clearing patterns.
- The cache is never authoritative for stock — the stock endpoint always
  reads/writes Postgres directly and invalidates the cache afterward, so
  caching cannot undermine the oversell guarantee from §3.3.

## 8. Edge Cases Covered

- Duplicate SKU or slug on create/rename → `409`
- Negative price/stock, or `compare_at_price < price` → `400`
- Category deleted while active products reference it → `409`
- Product deleted while it has active variants → cascades as soft-delete
- Concurrent stock decrements racing on the same variant → exactly the
  requests that fit succeed, the rest get `409` with the available quantity
- Concurrent full-record edits (two admins editing the same product) → `409`
  via `xmin`
- Oversized or malformed `attributes` JSON → `400` (size cap enforced in the
  validator)
- Invalid or expired pagination cursor → `400`
- Case-insensitive/unicode-safe slugs and search (`citext`)
- Repeated stock-decrement retries with the same `Idempotency-Key` → deduped,
  no double-decrement

## 9. Deliverables

- **Code**: ASP.NET Core Web API solution, EF Core migrations,
  `docker-compose.yml` (api + postgres + redis), README with setup/run
  instructions.
- **Postman collection**: JSON export covering every endpoint including
  example error responses (`400`/`404`/`409`), plus a Postman environment
  file for variables.
- **Environment variables**: `ConnectionStrings__Default`,
  `Redis__ConnectionString`, `ApiKey`, `ASPNETCORE_ENVIRONMENT`.
- **Design doc**: this spec, covering approach, DB rationale, schema, API
  reference, and performance/concurrency notes.
- **Limitations & future improvements** (documented, not built): real auth
  (JWT + RBAC), read replicas / PgBouncer for DB scale-out, multi-category
  support, Elasticsearch-grade search, CI/CD pipeline, rate limiting, an
  outbox pattern if stock-change events ever need to be published to other
  services.

No `gh` CLI is authenticated in this environment, so the repository will be
fully built and committed locally with git; the final `git push` to the
user's own GitHub is a manual step outside this session.

## 10. Explicitly Out of Scope

Cart, checkout, orders, payments, product images (removed from scope during
design review), real authentication/authorization, CI/CD pipeline.
