# Product Management Front-End — Design Spec

Date: 2026-08-21
Status: Approved for implementation

## 1. Context & Goal

Build the front-end for the product management assessment, integrating with
the backend API defined in
[`2026-08-20-product-management-api-design.md`](2026-08-20-product-management-api-design.md).
Evaluation criteria: project structure, UI layout, technology stack
(state management, API client, file uploader, UI framework), API/data
handling, and performance (error handling, lazy loading, code splitting).

**Scope**: full CRUD admin interface — product list/search/filter, product
create/edit (core fields + variants + single image), category
list/create/edit (with parent hierarchy), and stock adjustment. No
cart/checkout/storefront — this is the same product-management surface as
the backend spec, not a customer-facing shop front.

**Depth target**: same take-home realistic scope as the backend — fully
working UI wired to real endpoints, no login/auth UI (the backend has none
to integrate with — a deliberate, consistent decision, not an oversight),
no CI/CD.

## 2. A Scope Gap Found and Closed During Design

The assessment's evaluation criteria names a "file uploader" as a required
technology stack component, but the backend spec originally had **no**
image upload endpoint at all (images were cut from backend scope entirely
during that design phase, as adding no insight into the
consistency/scalability story being tested there).

Resolution: the backend spec was revisited and a deliberately minimal
single-image-per-product upload endpoint was added
(`POST /products/{id}/image`, `DELETE /products/{id}/image` — see that
spec's §3.2 and §7) specifically so this front-end's file uploader
component has something real to integrate with, rather than a mocked
upload with no backend behind it. Local disk storage, not real blob
storage — consistent with that spec's "minimal, not production-grade"
philosophy elsewhere (§11 there).

## 3. Technology Stack

- **Framework**: React 19 + TypeScript, built with **Vite**
- **Routing**: React Router, with lazy-loaded route modules (§7)
- **State management**: **Redux Toolkit + RTK Query** — RTK Query owns all
  server state (products/categories/variants: caching, invalidation,
  loading/error states per query) so there's no hand-rolled data-fetching
  logic; plain Redux slices are used only where genuine client-only UI
  state exists (e.g. table filter selections), not for anything the API
  already owns.
- **UI component library**: **Ant Design (antd)** — chosen specifically
  because its `Table`, `Form`, and `Upload` components map directly onto
  this assessment's three biggest UI needs (data-dense product list,
  validated forms, file uploader) without assembling those from
  lower-level primitives.
- **API client**: a hand-written **Axios** client, used as RTK Query's
  custom `baseQuery` (not RTK Query's default `fetchBaseQuery`) —
  specifically because two cross-cutting concerns are naturally expressed
  as Axios interceptors and awkward otherwise:
  - **Request interceptor**: attaches `If-Match` automatically from a
    cached `ETag` (§5) on every `PUT`/`PATCH`
  - **Response interceptor**: parses the backend's `ProblemDetails` error
    body into one normalized `AppError` shape before RTK Query ever sees
    it (§5)
- **Form validation**: Ant Design `Form`'s built-in validation rules — no
  separate schema library. Sufficient for this scope (required fields,
  numeric bounds, cross-field comparisons like `compareAtPrice >= price`,
  SKU pattern), and integrates natively with antd's form state/error
  display rather than needing a resolver adapter.
- **Testing**: Vitest + React Testing Library for component tests, **MSW**
  (Mock Service Worker) to mock API responses — tests never hit a real
  backend.

## 4. Project Structure

Feature-based, not layer-based — each feature folder owns its API calls,
types, and components together, mirroring how RTK Query itself is designed
to be used (one `createApi` slice per feature) and roughly mirroring the
backend's own resource grouping (§7 there: Categories/Products/Variants):

```
src/
  app/
    store.ts              # Redux store, RTK Query API slice registration
    router.tsx              # Route tree, lazy-loaded route modules
  features/
    products/
      api.ts                 # RTK Query endpoints: listProducts, getProduct,
                              # createProduct, updateProduct, deleteProduct,
                              # adjustStock, uploadImage, deleteImage
      types.ts                # TS interfaces matching backend DTOs
      ProductListPage.tsx      # sidebar shell content: filters + data table
      ProductDetailPage.tsx     # single stacked page: fields + image + variants
      ProductForm.tsx
      VariantsTable.tsx          # inline stock +/- controls
      ImageUploader.tsx           # antd Upload wrapper
    categories/
      api.ts
      types.ts
      CategoryListPage.tsx        # tree table (§6)
      CategoryForm.tsx             # includes parent tree-select
  shared/
    components/
      AppLayout.tsx              # sidebar shell (§6)
      ErrorBoundary.tsx
      EmptyState.tsx
      ConfirmModal.tsx
    hooks/
      useDebouncedValue.ts
    lib/
      axiosClient.ts              # one configured Axios instance + interceptors
      apiBase.ts                   # createApi() using axiosClient as baseQuery
      errors.ts                     # ProblemDetails -> AppError normalization
  main.tsx
```

## 5. API & Data Handling

**RTK Query cache invalidation** is tag-based, mapped directly onto backend
resources: creating a variant invalidates the `Product` tag for its parent
(so the detail view refetches); adjusting stock invalidates only that one
variant's tag, not the whole product.

**Closing the `ETag`/`If-Match` loop** (backend §7): the Axios response
interceptor captures the `ETag` header from every `GET`/write response and
stores it alongside the cached DTO — no separate store needed, it rides
along in RTK Query's own cache entry. The request interceptor attaches it
as `If-Match` automatically on `PUT`/`PATCH` for that resource. Component
code calls a normal "update product" mutation; the concurrency plumbing is
invisible to it.

**Error handling — one normalized shape everywhere**: the Axios response
interceptor parses the backend's `ProblemDetails` body into one `AppError`
(`status`, `message`, `fieldErrors?`, `traceId?`) before RTK Query surfaces
it via each hook's `error` field:
- **`400` with field-level validation errors** → mapped into antd
  `Form.setFields()`, showing the exact backend validation failure inline
  on the right form field
- **`404`/`409`/`500`** → antd `notification` toast; `500`s show the
  `traceId` (backend §7 always includes one) so a bug report can reference
  the exact server-side log entry
- **`409` specifically gets its own UX**, not a generic toast — see §8

**Client-side validation mirrors, never replaces, backend validation**:
antd Form rules catch obvious cases before a request is sent, purely to
save a round trip. The backend remains the actual source of truth — the
`400` field-error mapping above exists as a second line of defense, not a
redundant one, because the frontend never assumes client-side validation
alone is sufficient.

**Optimistic stock updates**: adjusting stock (backend §3.4's atomic
endpoint) uses RTK Query's `onQueryStarted` to update the cached stock
number in the table instantly, rolling back automatically on failure (e.g.
a `409` because a concurrent decrement won the race). The UI feels instant;
the backend stays fully authoritative — a rollback just means the
optimistic guess was wrong, never that bad data was written anywhere.
Every stock-adjust request generates a fresh `Idempotency-Key` (UUID),
reused only if the exact same attempt is auto-retried, matching backend
§7's dedup mechanism.

**Cursor pagination, represented honestly**: backend §7 uses cursor
(keyset) pagination — no "jump to page 50," by design, for scalability
(backend §4). The product table uses **Next/Previous buttons**, not antd's
default numbered pager. The frontend keeps a small stack of previously-seen
cursors so "Previous" works, but there's no page-jump control — an honest
reflection of what the backend actually supports, not a faked numbered
pager sitting on top of it.

## 6. UI Layout

Confirmed via visual mockups during design review — three layout decisions:

**App shell — sidebar layout.** Fixed left sidebar (Products / Categories
nav items), header shows breadcrumb + page title, main content area fills
the rest. Standard admin-dashboard shape, and what Ant Design's own layout
components (`Layout`, `Sider`) are built for.

**Product list — data table**, not a card grid. Toolbar above the table
(search input, category filter, status filter, "New Product" button),
antd `Table` below (image thumbnail, name, category, price range, total
stock, status, actions), Next/Previous pagination (§5). Chosen over a
visual card grid because an admin managing potentially thousands of SKUs
(backend's seed data goes up to 50,000+, §10 there) needs to scan rows
quickly and spot low-stock items at a glance — information density beats
visual browsing for this audience.

**Product detail/edit — single stacked page**, not tabs. Image + core
fields (name, category, brand, status, attributes) at the top, variants
table immediately below with inline stock `+`/`-` controls. No
tab-switching — chosen because seeded variant counts are small (2–4 per
product on average, backend §10), so the page never gets unreasonably long,
and everything relevant to editing one product stays visible at once
without navigating between views.

**Category list — tree table**, not a flat list with a breadcrumb column.
antd `Table`'s tree-data mode, expand/collapse rows, indentation shows the
parent/child hierarchy directly — matching the backend's self-referencing
`parent_category_id` (backend §3.2) structurally, not just visually. "New
Category" opens a form with a searchable tree-select for choosing the
parent.

## 7. Performance

- **Route-based code splitting**: every page (`ProductListPage`,
  `ProductDetailPage`, `CategoryListPage`) is a `React.lazy()` module
  loaded via React Router's lazy route support — the initial bundle ships
  only what the first screen needs.
- **Debounced search**: the product search input debounces ~350ms before
  firing the RTK Query request — without it, typing "cotton dress" against
  a 5,000+ seeded catalog fires a dozen separate search requests instead
  of one.
- **Table virtualization — deliberately not added**: backend §7 caps
  `limit` at 100 rows per page, so the list table never renders more than
  100 rows at once regardless of catalog size. antd's default
  (non-virtualized) `Table` handles that comfortably; virtualization would
  solve a problem the API's own pagination cap already prevents.
- **Two-layer caching**: RTK Query's in-memory cache sits on top of the
  backend's Redis cache (backend §8) — paging back to a previously-viewed
  product or list doesn't generate a network request, let alone hit
  Postgres.
- **Image lazy loading**: `<img loading="lazy">` on every product
  thumbnail — native browser behavior, no library needed.
- **Bundle size**: antd's ES module build tree-shakes correctly under Vite
  by default — no `babel-plugin-import`-style workaround needed (a
  Create React App-era fix for a different bundler, not applicable here).
- **Memoization**: `React.memo` on `VariantsTable` rows, `useMemo` for
  derived values (formatted price ranges, stock badges), `useCallback` for
  handlers passed into memoized children.
- **Prefetch on hover**: RTK Query's `prefetch()` fires on product-row
  hover in the list table, so clicking through to detail is often instant.

## 8. Edge Cases Covered

- **Concurrent edit conflict** (`409` from a stale `ETag`/`xmin` mismatch,
  backend §3.4): dedicated UX — "This product was changed by someone else"
  with a **Reload latest** action, not a generic error toast
- **Insufficient stock** (`409` on adjust): shows the backend's returned
  available quantity directly ("Only 3 left — you requested 5")
- **File validation before upload**: antd `Upload`'s `beforeUpload` hook
  checks type (`jpeg`/`png`/`webp`) and size (5 MB) client-side, matching
  backend §7's rules exactly — instant feedback instead of a round trip
- **Empty states**: no results for a filter/search → antd `Empty`
  component with a clear message, never a silently blank table
- **Broken/failed image load**: `onError` fallback on `<img>` swaps to a
  placeholder icon
- **Network failure / API unreachable**: a retry-able error state (RTK
  Query's built-in error state + a manual "Retry" button), never an
  infinite spinner or blank page
- **Filters and pagination survive refresh/back-forward**: search query,
  category filter, and the current cursor are synced to the URL via
  `useSearchParams` — refresh, browser back, or a shared link preserves
  exactly what was being looked at
- **Render-time crash containment**: a top-level `ErrorBoundary` around the
  route outlet catches unexpected render exceptions, shows a fallback UI
  instead of a blank white screen
- **Category delete blocked** (`409` from backend if active products
  reference it): surfaced as a clear message naming the constraint, not a
  generic failure

## 9. Deliverables

- **Repository structure**: same repo as the backend, sibling folders —
  `backend/` (the ASP.NET Core solution) and `frontend/` (this app), with
  `docker-compose.yml` at the repo root orchestrating all four services
  (`postgres`, `redis`, `api`, and a new `web` service) so one clone plus
  one command gets a reviewer a fully running app, not just an API.
- **`web` service**: multi-stage Dockerfile — Vite build stage, then
  served as static files via nginx. `depends_on: api` with a health
  condition.
- **Browser-reachable API URL, not the Docker-internal one**: the frontend
  is static files served to the *browser*, so its API base URL must be
  something the browser can reach (`http://localhost:<api-host-port>/api/v1`),
  not the internal Docker network hostname (`http://api:8080`) that only
  resolves container-to-container. Baked in at build time via
  `VITE_API_BASE_URL` — named here explicitly because it's a common
  docker-compose-plus-SPA mistake to get backwards.
- **Local dev workflow**: `docker compose up postgres redis api` for the
  backend, then `npm run dev` on the host for the frontend — fast Vite HMR
  loop for actual development. `docker compose up` (all services) brings
  up the built `web` service too, for a full one-command demo matching
  what a reviewer would run.
- **Environment variables**: `VITE_API_BASE_URL` (build-time).
- **README**: setup/run instructions (both workflows above), env vars,
  known limitations.
- **Limitations & future improvements** (documented, not built): no login
  UI (mirrors the backend's no-auth decision — consistent, not an
  oversight); no real-time updates (another user's stock change isn't
  reflected until refetch — WebSocket/SSE noted as a future improvement,
  same spirit as the backend's own future-improvements list); no E2E tests
  (Playwright/Cypress) — only unit/component coverage now; no i18n; default
  antd theme, no custom branding polish.

## 10. Explicitly Out of Scope

Cart, checkout, storefront/customer-facing views, login/authentication UI,
real-time updates, E2E test suite, CI/CD pipeline, multi-language support,
custom visual branding/theming.
