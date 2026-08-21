# Product Management API — Design Spec

Date: 2026-08-20
Status: Approved for implementation

## 1. Context & Goal

Build product management endpoints for a retail/e-commerce application (fashion shop
example), for a take-home assessment. Evaluation criteria: development approach,
database design (SQL vs NoSQL rationale, extensibility), tech stack (ORM, validation),
API/data handling, and performance (caching, concurrency).

**Scope**: core product catalog management — products, categories, variants
(size/color/SKU/price/stock), with concurrency-safe stock updates, plus a
deliberately minimal single-image-per-product upload (§3.2, §7) added
later specifically to give a companion front-end spec's file-uploader
component something real to integrate with. Explicitly **out of scope**:
cart, checkout, orders, payments, multi-image galleries, and real blob
storage for images (local disk only — see §11).

**Depth target**: take-home realistic scope — fully working API, DB, and tests for
the in-scope features; no authentication/authorization or CI/CD pipeline. Both
are documented as future improvements rather than built (see §11).

## 2. Development Approach

1. Design the schema and consistency strategy first (this doc) — the hardest
   decisions (SQL vs NoSQL, how stock avoids overselling) shape everything else.
2. Scaffold the solution as four Clean Architecture projects (Domain,
   Application, Infrastructure, Api), plus the Postgres/Redis docker-compose
   environment.
3. Implement endpoints bottom-up: categories → products → variants → stock,
   each with validation, error handling, and tests before moving to the next.
4. Add caching once the read endpoints exist and are correct — caching is a
   performance layer on top of correct behavior, not a substitute for it.
5. Write integration tests for the concurrency-critical path (parallel stock
   decrements) against the real Postgres started by docker-compose (§10),
   not mocks — consistency guarantees are meaningless if only tested against
   an in-memory fake.
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
  id             bigint identity PK,
  name           varchar(200)  NOT NULL,
  slug           citext        NOT NULL UNIQUE,
  description    text          NULL,
  category_id    bigint        NOT NULL FK -> categories(id),
  brand          varchar(100)  NULL,
  status         smallint      NOT NULL DEFAULT 0,  -- 0=Draft,1=Active,2=Archived
  attributes     jsonb         NOT NULL DEFAULT '{}',
  image_url      varchar(500)  NULL,
  search_vector  tsvector      GENERATED ALWAYS AS (
                   setweight(to_tsvector('english', coalesce(name, '')), 'A') ||
                   setweight(to_tsvector('english', coalesce(brand, '')), 'B') ||
                   setweight(to_tsvector('english', coalesce(description, '')), 'C')
                 ) STORED,
  created_at     timestamptz   NOT NULL DEFAULT now(),
  updated_at     timestamptz   NOT NULL DEFAULT now()
  -- xmin (Postgres system column) used as EF Core concurrency token
)
-- indexes: unique(slug), btree(category_id), btree(status),
--          GIN(attributes jsonb_path_ops), GIN(search_vector),
--          GIN trigram(name) via pg_trgm — typo-tolerant fallback, see §3.3

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

**Images — deliberately minimal**: `products.image_url` is a single
nullable column, not a separate `product_images` table. Product images
were originally cut from scope entirely (§1) because they added no insight
into the consistency/scalability story this assessment tests — that
reasoning still holds, so this stays as small as possible: **one** image
per product, stored on local disk (not real blob storage), no per-variant
images, no ordering/gallery. It exists specifically to give the front-end
(separate spec) a real upload endpoint to integrate its file-uploader
component against, rather than a mocked one. See §7 for the endpoint and
§10 for storage/config details; real blob storage (S3/Azure Blob) remains
a documented future improvement (§11), not this.

### 3.3 Search Strategy

`q` on `GET /products` (§7) searches `name`, `brand`, and `description`
together via `products.search_vector` — a generated, always-in-sync
`tsvector` column (Postgres computes it automatically on every
insert/update, no application code keeps it current), weighted `name` >
`brand` > `description` so a match in the product name ranks above an
incidental mention buried in the description. Matched via
`search_vector @@ websearch_to_tsquery('english', :q)` (handles multi-word
queries, phrases in quotes, `-exclude` terms — the same query syntax users
already expect from a search box), results ordered by `ts_rank(search_vector,
query)` — actual relevance ranking, not just filter-then-sort-by-date.

**Typo fallback**: full-text search doesn't tolerate misspellings (`ts_query`
matches lexemes, not approximate strings). If the full-text query returns
zero rows, the API falls back to the trigram similarity index on `name`
(`pg_trgm`) — catching the case where a customer typed "tshirt" or a
minor misspelling that full-text search's exact-lexeme matching would miss
entirely. Fallback only fires on zero results, so it never overrides a
real ranked full-text match with a fuzzier, less precise one.

**Deliberately still Postgres-only**: this is a meaningful upgrade over a
single `ILIKE`/trigram-only search without introducing new infrastructure —
Elasticsearch/vector search remains a documented future improvement (§10)
for when result-ranking sophistication or catalog scale genuinely outgrows
what Postgres full-text search can do.

### 3.4 Consistency Strategy

Two **distinct** concurrency-control categories, deliberately not the same
mechanism reused twice — matched to each write path's contention profile
rather than picking one and applying it everywhere:

**Stock changes (the concurrency-critical path) — atomic conditional
update, a single-statement compare-and-swap.** This is its own category,
not a variant of optimistic concurrency below: there is no separate
read-then-compare-then-write cycle at all, so there is nothing to retry.
Issued via EF Core's `ExecuteUpdateAsync` (no hand-written SQL, no
load-then-save):
```csharp
var affected = await db.ProductVariants
    .Where(v => v.Id == id && v.StockQuantity >= qty)
    .ExecuteUpdateAsync(s => s.SetProperty(
        v => v.StockQuantity, v => v.StockQuantity - qty));
```
which compiles to `UPDATE product_variants SET stock_quantity =
stock_quantity - :qty WHERE id = :id AND stock_quantity >= :qty`. Zero rows
affected means insufficient stock → `409 Conflict`. Because the arithmetic
happens in the database against the row's current value — never read into
application memory first — there is no window for a concurrent request to
read a stale value and compute a conflicting result (the classic
lost-update problem a load-then-`SaveChanges()` pattern is exposed to).
Postgres's row-level lock during the statement itself is sufficient; no
explicit transaction, long-held lock, or `SERIALIZABLE` isolation is
needed — the statement is atomic on its own. This is deliberately neither
classic optimistic concurrency (no retry loop needed, so no wasted
round-trips under heavy contention) nor pessimistic locking (no lock held
across multiple statements) — it gets the low overhead of the former and
the correctness guarantee of the latter, for exactly the shape of problem
"decrement this counter, but never below zero."

**General field edits (name, price, category, etc.) — optimistic
concurrency**, via Postgres's native `xmin` system column exposed to EF
Core as a concurrency token: read the row (capturing its current `xmin`),
edit it, then write with `WHERE id = :id AND xmin = :expectedXmin`.
Conflicting concurrent edits return `409` with the current server state so
the client can reload and retry. This category assumes conflicts are rare
and cheap to retry — true for admin catalog edits, false for stock
decrements under load, which is exactly why stock doesn't use this
mechanism.

**Why not one mechanism for both**: the two write paths have opposite
contention profiles. Stock decrements can be hit by many concurrent
requests racing for the same row (a flash sale); general field edits are
low-frequency, human-paced admin activity where an occasional retry is
cheap. Using optimistic concurrency for stock would mean retry storms
under exactly the load pattern that matters most; using the atomic
conditional-update pattern for general edits would work but adds no value
over the simpler read-modify-write-with-a-version-check flow, since
there's no hot-path performance pressure to justify collapsing it into one
statement.

| | Atomic conditional update (stock) | Optimistic concurrency (`xmin`, general edits) |
|---|---|---|
| Read-then-write cycle? | No — one statement | Yes — read, mutate, write |
| Behavior on conflict | `0` rows affected, no retry — caller gets a definitive `409` | Throws, caller must reload and retry |
| Lock held across round-trips? | No | No (optimistic — no lock at all, just a version check at write time) |
| Best suited to | High contention, single-field numeric change | Low contention, multi-field edits |

Both write paths surface conflicts as `409` uniformly, so API consumers need
only one conflict-handling code path regardless of which category is
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
  attribute filter, full-text search — §3.3) so query cost stays flat as
  row count grows.
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

## 5. Solution Architecture (Clean Architecture)

Four projects, dependencies pointing inward only:

- **`ProductManagement.Domain`** — entities (`Product`, `Category`,
  `ProductVariant`), enums, domain exceptions. Zero external dependencies —
  no EF Core, no ASP.NET references. This is where the invariants that must
  always hold live (e.g. stock can't go negative), independent of how they're
  enforced by any particular database.
- **`ProductManagement.Application`** — use cases (e.g.
  `CreateProductHandler`, `AdjustStockHandler`), request/response DTOs,
  FluentValidation validators, and the interfaces the use cases depend on
  (`IProductRepository`, `IVariantRepository`, `IStockRepository`,
  `ICategoryRepository`, `ICacheService`, `IUnitOfWork`, `IFileStorageService`).
  Depends only on Domain. This is where business rules and orchestration
  live — no SQL, no HTTP, no filesystem. `IVariantRepository` and
  `IStockRepository` are deliberately separate: `IVariantRepository` is
  general variant CRUD (create, update non-stock fields, soft-delete),
  while `IStockRepository` exposes only the single atomic adjustment
  method from §3.4 — keeping `AdjustStockHandler`'s dependency surface down
  to exactly the one method it needs, easier to fake with NSubstitute and
  impossible to misuse for a non-atomic stock write by accident.
  `IFileStorageService` (`SaveAsync(stream, fileName) -> url`,
  `DeleteAsync(url)`) is the abstraction behind the image endpoint — the
  product-image use case depends on this interface, never on "local disk"
  directly, so swapping in real blob storage later (§11) means writing one
  new Infrastructure implementation, zero Application changes.
- **`ProductManagement.Infrastructure`** — EF Core `DbContext` + entity
  configurations + migrations, repository implementations, the
  `ExecuteUpdateAsync`-based atomic stock update, an `IUnitOfWork`
  implementation wrapping the `DbContext` (EF Core's `DbContext` already
  tracks multiple entities and commits them together via `SaveChangesAsync`,
  so `IUnitOfWork` here is a thin Application-facing interface over that
  existing behavior — not a second change-tracking layer), Redis-backed
  `ICacheService` implementation, and a local-disk `IFileStorageService`
  implementation (writes under a mounted volume — §10). Depends on
  Application (implements its interfaces) and Domain.
- **`ProductManagement.Api`** — controllers, middleware (`ProblemDetails`
  error mapping), Swashbuckle setup, and the composition root
  (`Program.cs`) that wires Infrastructure implementations to Application
  interfaces via DI. Controllers depend only on Application (use cases/DTOs)
  — never directly on EF Core or Infrastructure types.

**Why**: keeps framework/DB-specific concerns (EF Core, Redis, ASP.NET) out
of business logic, so the atomic-stock-update rule and validation logic are
unit-testable without a running web host or a real database, and swapping
Postgres or Redis later wouldn't ripple into Application or Domain.

**Where `IUnitOfWork` is used, and where it isn't**: use cases that write
across more than one repository in a single logical operation — e.g.
`CreateProductHandler`, which creates a product and its initial
`variants[]` together — call `IUnitOfWork.SaveChangesAsync()` once after
both repository calls, so either both persist or neither does.
`AdjustStockHandler` (§3.4) deliberately does **not** go through
`IUnitOfWork` — its `ExecuteUpdateAsync` call is already a single atomic
statement against one row, so wrapping it in a unit-of-work transaction
would add overhead without adding any correctness it doesn't already have.

**Test project layout mirrors this split**: `ProductManagement.UnitTests`
(Domain + Application, no I/O, fast), `ProductManagement.IntegrationTests`
(targets the real Postgres/Redis started by `docker-compose.yml` — see §10
— exercising the atomic stock update and concurrency behavior end-to-end
against the same services used for manual local testing, no Testcontainers),
and `ProductManagement.ArchitectureTests` (below) — three projects, three
different things being verified.

**`ProductManagement.UnitTests` scope** — everything here runs with no
database, no cache, no network, so the suite stays fast enough to run on
every save:
- **Domain**: entity invariant checks — e.g. a `ProductVariant` rejects
  negative price/stock at construction, `Product.Archive()` throws if
  already archived — exercising the `DomainException` hierarchy from §7
  directly.
- **Application — validators**: FluentValidation rules — e.g.
  `CreateProductRequestValidator` rejects a missing name, negative price,
  `compare_at_price < price`, oversized `attributes` JSON (the edge cases
  already listed in §9).
- **Application — use-case handlers**: e.g. `CreateProductHandler`,
  `AdjustStockHandler` logic, with `IProductRepository` / `ICacheService` /
  `IUnitOfWork` faked out via **NSubstitute** rather than a real
  implementation — the handler's decision logic (how it turns a
  repository result into a success/conflict outcome) is what's under test,
  not the database itself.
- **Application — mapping**: `ToDto()` / `ToEntity()` extension methods map
  fields correctly, including null `attributes` / empty `variants[]` edge
  cases.

**Explicitly not unit tested** — these require the real docker-compose
Postgres/Redis to mean anything, and live in `IntegrationTests` instead:
the actual `ExecuteUpdateAsync` atomic behavior, EF Core query translation,
real Redis cache behavior. This is exactly why §3.4's concurrency guarantee
is proven at the integration layer, not mocked away.

### Architecture Tests — Enforcing the Dependency Rule

§5's "dependencies pointing inward only" rule is a design intent, not
something the compiler enforces on its own — nothing stops a future change
from adding a stray `using Microsoft.EntityFrameworkCore;` to a `Domain`
entity for convenience. Rather than rely on code review catching that every
time, `ProductManagement.ArchitectureTests` asserts the dependency graph
itself, using **`NetArchTest.Rules`** (reflects over compiled assemblies via
Mono.Cecil, integrates with xUnit like any other test):

```csharp
[Fact]
public void Domain_Should_Not_Depend_On_Other_Layers()
{
    var result = Types.InAssembly(typeof(Product).Assembly)
        .Should()
        .NotHaveDependencyOnAny(
            "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore",
            "ProductManagement.Application", "ProductManagement.Infrastructure")
        .GetResult();

    result.IsSuccessful.Should().BeTrue();
}
```

Rules asserted, one test per rule:
- `Domain` → no dependency on `Application`, `Infrastructure`, `Api`, EF
  Core, or ASP.NET Core
- `Application` → no dependency on `Infrastructure`, `Api`, EF Core, or
  ASP.NET Core
- `Infrastructure` → no dependency on `Api`
- Controllers in `Api` → no direct dependency on `Infrastructure` (catches
  a controller injecting a `DbContext` or a concrete repository instead of
  an Application interface — the exact mistake §5's "controllers depend
  only on Application" rule is meant to prevent)

This project necessarily references all four layer assemblies (`Domain`,
`Application`, `Infrastructure`, `Api`) to inspect the dependency graph
between them — unlike `UnitTests`, which by design only references
`Domain`+`Application`. These tests run alongside `UnitTests` in the normal
`dotnet test` pass — no database, no Docker, just reflection over the built
assemblies — so a dependency-rule violation fails fast, the same run that
would catch a broken unit test.

### Dependency Injection & Composition Root

Each project registers its own services rather than everything being wired
directly in `Program.cs`:
- `ProductManagement.Application` exposes `AddApplication()` — registers
  use-case handlers and FluentValidation validators
  (`AddValidatorsFromAssembly`). No external dependencies to configure.
- `ProductManagement.Infrastructure` exposes `AddInfrastructure(IConfiguration
  config)` — registers the EF Core `DbContext` (Npgsql, connection string
  from config), repository implementations, `IUnitOfWork`, and the Redis
  `IConnectionMultiplexer` + `ICacheService`.
- `ProductManagement.Api`'s `Program.cs` composes them:
  `builder.Services.AddApplication().AddInfrastructure(builder.Configuration);`
  plus Api-only concerns (Swashbuckle, the global exception middleware from
  §7, controllers). This keeps `Program.cs` a thin composition root instead
  of an ever-growing registration dump, and each layer's DI needs travel
  with the layer itself.

**Lifetimes, and the pitfall this avoids**: the `DbContext` is registered
`Scoped` (one instance per HTTP request — the ASP.NET Core default for
`AddDbContext`). Every repository implementation and `IUnitOfWork` is also
registered `Scoped`, resolving **the same `DbContext` instance** within a
request. This matters concretely: if `IUnitOfWork` ever resolved a
*different* `DbContext` than the repositories it's meant to coordinate,
`SaveChangesAsync()` would silently commit nothing meaningful — a common
failure mode in Clean-Architecture-plus-EF-Core setups, avoided here simply
by keeping both bound to one scoped registration. `ICacheService`'s
underlying `IConnectionMultiplexer` is registered `Singleton` instead —
it's an expensive-to-create, thread-safe connection meant to be reused
across the app's lifetime, not per-request state like the `DbContext` is.

**Constructor injection only, everywhere** — no service locator, no
`IServiceProvider` passed into a constructor. Controllers and use-case
handlers declare dependencies as constructor parameters typed against
Application/Domain interfaces; nothing outside Infrastructure/Api ever
references a concrete EF Core or Redis type.

## 6. Technology Stack

- **Framework**: ASP.NET Core Web API (.NET 10)
- **ORM**: EF Core 10 (`Microsoft.EntityFrameworkCore*` 10.0.11,
  `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3) — code-first migrations; `xmin` as
  native concurrency token; the stock update uses EF Core's
  `ExecuteUpdateAsync` bulk-update API (§3.4) for a LINQ-expressed, still
  fully-atomic conditional update — no hand-written/raw SQL anywhere in the
  codebase.
- **Persistence coordination**: Repository pattern (`IProductRepository`,
  `IVariantRepository`, `IStockRepository`, `ICategoryRepository` in
  Application, implemented in Infrastructure — §5 explains why variant CRUD
  and the atomic stock adjustment are split into two interfaces) plus a
  thin `IUnitOfWork` over `DbContext.SaveChangesAsync`
  for use cases that must commit writes across more than one repository
  atomically (§5).
- **Validation**: FluentValidation — one validator per request DTO. Chosen
  over DataAnnotations because several rules are cross-field (e.g.
  `compare_at_price >= price`), which DataAnnotations handles awkwardly.
- **Mapping**: manual extension methods (`ToDto()` / `ToEntity()`) — the
  entity/DTO shape difference is small enough that AutoMapper/Mapster would
  add indirection without saving real effort.
- **Caching**: `StackExchange.Redis`, cache-aside pattern.
- **Logging**: Serilog, structured JSON to console.
- **Testing**: xUnit, plus **NSubstitute** for faking repository/cache
  interfaces in unit tests (chosen over Moq, whose 2023 SponsorLink
  telemetry incident pushed much of the .NET community toward
  alternatives; see §5 for exact unit-test scope). Integration tests run
  against the real Postgres/Redis started by `docker-compose.yml` (§10)
  rather than Testcontainers — one less moving part, and integration tests
  exercise the exact same services manual testing uses. `Respawn` resets
  table state between test runs (deletes
  rows respecting FK order, re-seeds nothing) so tests stay isolated and
  repeatable against a persistent database instead of a fresh-per-run
  ephemeral one. This is required for the concurrency test specifically
  (fire N parallel stock-decrement requests, assert stock never goes
  negative and exactly the right number of requests succeed) — that
  guarantee is only meaningful proven against a real Postgres instance, not
  an in-memory fake. `NetArchTest.Rules` enforces the Clean Architecture
  dependency rule itself as a third, always-run test category (§5).
- **API docs**: Swashbuckle (OpenAPI/Swagger UI) generated from code, plus a
  separately maintained Postman collection for the submission.

## 7. API Design

Base path `/api/v1`, JSON throughout, designed to RESTful conventions:

- **Resources are nouns, not verbs** — `/products`, `/products/{id}/variants`,
  never `/getProducts` or `/createProduct`. Nesting reflects true ownership
  (`/products/{productId}/variants/{variantId}`), capped at two levels deep
  to avoid unwieldy URLs.
- **HTTP methods carry the meaning**: `GET` (safe, no side effects,
  cacheable), `POST` (create a subordinate resource), `PUT` (full,
  idempotent replace), `PATCH` (partial update, e.g. the stock sub-resource),
  `DELETE` (idempotent removal). `PUT`/`DELETE` are idempotent by design —
  repeating either has the same effect as calling it once.
- **Status codes are meaningful, not just 200/500**: `201 Created` with a
  `Location` header pointing at the new resource on every `POST` that
  creates something (`/categories`, `/products`, `/products/{id}/variants`);
  `200 OK` with body on `GET`/`PUT`/`PATCH`; `204 No Content` on `DELETE`;
  `400`/`404`/`409` for client-side error states (detailed below);
  never a bare `500` for an expected business condition (e.g. insufficient
  stock is `409`, not `500`).
- **Stateless**: no server-side session — a prerequisite for the horizontal
  read scaling in §4.
- **Filtering, sorting, and pagination are query parameters**, never part of
  the path (`GET /products?categoryId=3&cursor=...`), keeping the resource
  URL itself stable regardless of how it's queried.
- **HATEOAS is explicitly not implemented** — hypermedia links add
  complexity this assessment's scope doesn't call for; noted here as a
  deliberate omission rather than an oversight.

### Categories
- `GET /categories` — flat list; `?parentId=`, `?activeOnly=true`
- `GET /categories/{id}`
- `POST /categories` — `201 Created` + `Location: /categories/{id}`
- `PUT /categories/{id}`
- `DELETE /categories/{id}` — `409` if active products still reference it,
  else `204 No Content`

### Products
- `GET /products` — filters: `categoryId`, `status`, `q` (full-text search
  across name/brand/description, ranked, with a trigram typo-tolerant
  fallback — §3.3), `minPrice`/`maxPrice`, `attributes` (JSON containment
  match); cursor pagination (`?cursor=&limit=`, `limit` capped at 100).
  Without `q`, the cursor is keyed on `(created_at, id)` as usual; with `q`,
  it's keyed on `(ts_rank, id)` instead, so relevance-ordered results still
  paginate correctly (`id` breaks ties between equally-ranked rows) rather
  than falling back to the default recency order. Returns a lightweight
  list DTO (no variant/detail payload).
- `GET /products/{id}` / `GET /products/slug/{slug}` — full detail incl.
  variants; response carries an `ETag` header derived from the row's
  `xmin` (§3.4) — this is the version token a later `PUT`/`PATCH` echoes
  back via `If-Match`, closing the loop on the optimistic-concurrency flow.
  `POST`/`PUT`/`PATCH` responses carry the same header for the newly
  written state, so a client never has to re-`GET` just to get the next
  version token before its next write.
- `POST /products` — `201 Created` + `Location: /products/{id}`; body may
  include an initial `variants[]` array, created transactionally with the
  product
- `PUT /products/{id}` — full replace; requires `If-Match` with the version
  token; `409` on mismatch
- `PATCH /products/{id}` — partial update, same concurrency check
- `DELETE /products/{id}` — soft delete (`status = Archived`), `204 No
  Content`
- `POST /products/{id}/image` — `multipart/form-data`, single file field;
  validated server-side (content type must be `image/jpeg`, `image/png`,
  or `image/webp`; max 5 MB — checked in the Application handler before
  touching disk, not via FluentValidation, which isn't a natural fit for
  file streams). Saves to local disk (§10), sets `products.image_url`,
  returns `200 OK` with `{ "imageUrl": "/uploads/products/{id}/{guid}.jpg" }`.
  Replacing an existing image deletes the old file from disk before saving
  the new one — no orphaned files accumulate.
- `DELETE /products/{id}/image` — deletes the file from disk and clears
  `image_url`, `204 No Content`. `404` if no image is currently set.

### Variants
- `GET /products/{productId}/variants`
- `POST /products/{productId}/variants` — `201 Created` +
  `Location: /products/{productId}/variants/{variantId}`; `409` on
  duplicate SKU
- `PUT /products/{productId}/variants/{variantId}`
- `DELETE /products/{productId}/variants/{variantId}` — soft delete
  (`is_active = false`), `204 No Content`
- `PATCH /products/{productId}/variants/{variantId}/stock` — body
  `{ "delta": -3 }` (negative = decrement/sale, positive = restock);
  atomic conditional UPDATE per §3.4; optional `Idempotency-Key` header
  (checked against Redis, short TTL) so a retried request can't
  double-decrement stock; `200 OK` with the resulting stock level, or `409`
  with the available quantity if the decrement can't be satisfied

### Input/Output Handling

Every write: FluentValidation → business-rule check (FK existence,
uniqueness) → DB write inside a transaction → mapped response DTO. Errors
use **RFC 7807 `ProblemDetails`** uniformly: `400` validation, `404` not
found, `409` conflict (duplicate SKU/slug, concurrency, insufficient stock).
The full exception→status mapping is the table in "Error Handling" below —
every case in this design resolves to one of those three, so no other
4xx code is used. All responses use DTOs, never raw entities, so internal
schema changes never leak into the API contract.

### Error Handling & Global Exception Middleware

**Exception hierarchy, scoped to where each failure is actually detected:**
- `ProductManagement.Domain` defines a small typed hierarchy
  (`DomainException` base) for invariant violations an entity itself
  refuses to allow (e.g. attempting to archive an already-archived
  product). Domain stays framework-agnostic — it throws plain C# exception
  types, nothing ASP.NET-aware.
- `ProductManagement.Application` defines its own use-case-level exceptions:
  `EntityNotFoundException` (category/product/variant lookup miss),
  `DuplicateSkuException`, `DuplicateSlugException`.
- `ProductManagement.Infrastructure` repository implementations catch
  EF Core/Npgsql-specific exceptions (`DbUpdateConcurrencyException` for an
  `xmin` mismatch; `DbUpdateException` wrapping a Postgres unique-violation,
  SQLSTATE `23505`) and **re-throw as the Application-level typed
  exceptions above** — so nothing above Infrastructure ever needs to know
  EF Core or Npgsql exception types exist, preserving the Clean
  Architecture boundary from §5.
- **Insufficient stock is deliberately not an exception at all** — per
  §3.4, `ExecuteUpdateAsync` returning `0` affected rows is an expected,
  common outcome (a popular SKU legitimately selling out under load), not
  an error condition. It's handled as a plain return-value check in the
  handler, mapped directly to `409`, keeping the hot concurrency path free
  of exception-handling overhead for something that isn't exceptional.

**Global handling in `ProductManagement.Api`:** a single `IExceptionHandler`
implementation, registered via `app.UseExceptionHandler()`, is the only
place HTTP status codes get decided for thrown exceptions:

| Exception | Status | Notes |
|---|---|---|
| FluentValidation `ValidationException` | `400` | field-level error list in the `ProblemDetails` body |
| `EntityNotFoundException` | `404` | |
| `DuplicateSkuException` / `DuplicateSlugException` | `409` | |
| `DbUpdateConcurrencyException` (surfaces if a repository didn't translate it) | `409` | current server state included so the client can retry |
| anything else (unanticipated: bug, DB connection drop, etc.) | `500` | generic `ProblemDetails` body only — **no exception message or stack trace ever reaches the client** |

For the `500` case specifically: the full exception and stack trace are
logged server-side via Serilog at `Error` level, tagged with a `traceId`
(`HttpContext.TraceIdentifier`), and that same `traceId` is echoed back in
the `ProblemDetails` response's `extensions` field — enough for a bug
report to be correlated to the exact server-side log entry without ever
exposing internals to the caller.

## 8. Performance & Caching

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
  caching cannot undermine the oversell guarantee from §3.4.

## 9. Edge Cases Covered

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
- Search query with a typo/misspelling that full-text search misses →
  trigram fallback still returns results (§3.3)
- Search query matching nothing at all (both full-text and trigram) →
  `200` with an empty result set, not `404` (an empty list is a valid,
  successful answer to "search for X")
- Image upload with wrong content type (e.g. a PDF) or oversized (>5 MB) →
  `400`, nothing written to disk
- Image upload replacing an existing image → old file deleted from disk
  before the new one is saved, no orphaned files
- Image delete when no image is set → `404`

## 10. Deliverables

- **Code**: Clean Architecture solution (Domain / Application / Infrastructure
  / Api projects per §5) with EF Core migrations, `docker-compose.yml`
  (api + postgres + redis), README with setup/run instructions.
- **Postman collection**: JSON export covering every endpoint including
  example error responses (`400`/`404`/`409`), plus a Postman environment
  file for variables.
- **Environment variables**: `ConnectionStrings__Default`,
  `Redis__ConnectionString`, `ASPNETCORE_ENVIRONMENT`, `Seeding__CategoryCount`,
  `Seeding__ProductCount`, `Seeding__MaxVariantsPerProduct` (see Seed Data,
  below — all optional, default to the seeder's built-in values if unset).
- **Design doc**: this spec, covering approach, DB rationale, schema, API
  reference, and performance/concurrency notes.
- **Limitations & future improvements** (documented, not built): **no
  authentication/authorization is implemented — all endpoints are public**;
  production would require JWT + RBAC gating write endpoints. Product
  images are stored on local disk, not real blob storage — fine for one
  container, not for a horizontally-scaled deployment (every replica would
  need the same volume, or images uploaded to one replica would be
  invisible to requests served by another); production would need
  S3/Azure Blob behind the existing `IFileStorageService` abstraction (§5),
  which is exactly what that interface is already shaped to make a
  same-Application, Infrastructure-only change. Also: read replicas /
  PgBouncer for DB scale-out, multi-category support, Elasticsearch-grade
  search, CI/CD pipeline, rate limiting, an outbox pattern if stock-change
  events ever need to be published to other services.

### Local Development & Testing Workflow

`docker-compose.yml` brings up three services and is the **single**
local-testing setup — both manual testing and the automated integration
suite target it, no Testcontainers involved:
- `postgres` (matching the production-targeted major version) — named
  volume so data survives restarts, healthcheck gating readiness
- `redis` — healthcheck gating readiness
- `api` — built from a local `Dockerfile`, `depends_on` both services with
  `condition: service_healthy` so it never starts before its dependencies
  can accept connections; applies EF Core migrations automatically at boot
  (`dbContext.Database.MigrateAsync()`), then seeds sample data (below),
  both gated to non-`Production` environments only, so neither behavior can
  ever run against a real deployment by accident. A named volume
  (`uploads_data`) is mounted at `/app/wwwroot/uploads`, and
  `app.UseStaticFiles()` serves that directory directly — so an uploaded
  product image (§7) survives container restarts and is reachable at
  `http://localhost:<port>/uploads/products/{id}/{file}` for the front-end
  to render in an `<img>` tag, no separate file-serving service needed.

**Workflow**: `docker compose up --build` (or `-d` to run detached) brings
up the full stack; Swagger UI is reachable at
`http://localhost:<port>/swagger` for interactive exploration, and the
delivered Postman collection's environment file points at the same base
URL — so the same running stack serves manual Swagger poking, the Postman
collection, and `dotnet test` for the integration project. `docker compose
down -v` tears everything down including volumes, for a clean-slate
restart.

### Seed Data

`DbInitializer.SeedAsync()` runs immediately after the auto-migrate step,
gated to non-`Production` environments only, and only when
`await db.Products.AnyAsync() == false` — so it's a no-op on every restart
after the first, without needing a separate "have I seeded before" flag.

**Scale, configurable via `Seeding__*` env vars** so it can be dialed up
for heavier performance testing without a code change: `CategoryCount`
(default 40 — 8 top-level × ~5 children), `ProductCount` (default 5,000),
`MaxVariantsPerProduct` (default 2–4, randomized) → ~12,500 variants by
default. Bump `ProductCount` to 50,000+ later to stress-test search
ranking and cursor pagination at real scale.

**Realistic data via `Bogus`**, seeded with a fixed random seed so a
regenerated dataset is deterministic — real-sounding fashion product
names, brands, descriptions, and `attributes` JSON (material/fit/care),
not `"Product 1"`/`"Product 2"`. This matters specifically for exercising
§3.3's search ranking meaningfully. Products are assigned only to leaf
categories, never the ~8 top-level container categories, matching how
browsing actually works.

**Insert performance**: batched (~1,000 rows per `SaveChangesAsync()`)
with `ChangeTracker.AutoDetectChangesEnabled = false` during the loop —
plain per-row `AddRangeAsync`/`SaveChangesAsync` would be slow at this
volume purely from EF Core's change-tracking overhead. If `ProductCount`
is ever pushed into the millions for heavier benchmarking, Npgsql's binary
`COPY` API is the next lever — a documented option, not built now.

**Exact lifecycle behavior** — this is keyed on *"is there data,"* not
*"has seeding run before,"* which produces different outcomes depending on
how the stack is restarted:
- `docker compose stop` then `up` again (container restarted, volume
  intact): `products` still has rows → seeder skips. This is the common
  case — seed once, then it's just there.
- `docker compose down -v` (volumes removed) then `up`: Postgres data is
  wiped along with the volume → `products` is empty again → **the seeder
  runs again**, regenerating the same deterministic dataset. This is
  intentional — `down -v` is exactly how you'd reset to a clean slate for
  a fresh performance-testing run.
- Manually deleting rows without restarting the app (e.g. `DELETE FROM
  products` by hand mid-session): the check only runs at startup, not
  continuously, so it won't auto-reseed until the `api` process actually
  restarts (`docker compose restart api`, or a full `up` again).

**Keeping automated tests isolated on a persistent (not ephemeral)
database**: since `IntegrationTests` runs against the same long-lived
`postgres` container repeatedly rather than a fresh one per run, two things
keep it safe and repeatable:
- It targets a **separate database** inside the same Postgres instance
  (`productdb_test` vs. the app's `productdb`) via its own connection
  string, so running tests never touches data you're manually poking at
  through Swagger/Postman in the same compose session.
- `Respawn` resets `productdb_test`'s table state between test runs
  (deletes all rows respecting FK order) so each test run starts from a
  known-clean schema without needing to tear down and recreate the
  container.

This trades Testcontainers' automatic ephemeral-container-per-run isolation
for one less moving part locally — everything, manual and automated, runs
against the one `docker compose up` stack.

No `gh` CLI is authenticated in this environment, so the repository will be
fully built and committed locally with git; the final `git push` to the
user's own GitHub is a manual step outside this session.

## 11. Explicitly Out of Scope

Cart, checkout, orders, payments, real authentication/authorization,
CI/CD pipeline. Product images are **partially** in scope (§3.2, §7) — a
single image per product on local disk, added specifically for the
front-end's file uploader — but multi-image galleries, per-variant
images, image resizing/thumbnails, and real blob storage (S3/Azure Blob)
remain out of scope, documented as future improvements.
