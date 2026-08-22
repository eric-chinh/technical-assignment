# Product Management Front-End Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the React admin front-end defined in [`2026-08-21-product-management-frontend-design.md`](../specs/2026-08-21-product-management-frontend-design.md), integrating against the real backend API from the companion backend implementation plan.

**Architecture:** Feature-based structure (spec §4) — Redux Toolkit + RTK Query for all server state, a hand-written Axios client as RTK Query's `baseQuery` for the `ETag`/`If-Match` and error-normalization interceptors, Ant Design for UI, React Router with lazy-loaded routes. Built bottom-up: shared plumbing (Axios client, store) → app shell → Categories (simpler, establishes the pattern) → Products (list, detail, variants, image, stock) → cross-cutting edge cases → Docker/README wrap-up.

**Tech Stack:** React 19, TypeScript, Vite, Redux Toolkit + RTK Query, Ant Design (antd), Axios, React Router, Vitest + React Testing Library + MSW.

---

## Conventions Used Throughout This Plan

- App root: `frontend/`
- Dev server port: `5173` (Vite default, also the `web` docker service's port — spec §9)
- Backend API base URL: `http://localhost:8080/api/v1` (backend plan's pinned port)
- Backend response shapes referenced below come directly from the backend implementation plan's DTOs (`ProductDto`, `CategoryDto`, `VariantDto`, `PagedResult<T>`, `AdjustStockResult`) — ASP.NET Core serializes these as camelCase JSON by default, which is what every TypeScript interface below matches
- All commands assume the working directory is `frontend/` unless stated otherwise

---

## Task 0: Project Scaffolding

**Files:**
- Create: `frontend/package.json` (via Vite scaffold)
- Create: `frontend/tsconfig.json`, `frontend/vite.config.ts`
- Create: `frontend/.env`
- Create: `frontend/src/main.tsx`, `frontend/index.html`

- [ ] **Step 1: Scaffold the Vite + React + TypeScript project**

```bash
npm create vite@latest frontend -- --template react-ts
cd frontend
npm install
```

- [ ] **Step 2: Install runtime dependencies**

```bash
npm install @reduxjs/toolkit react-redux axios antd react-router-dom
```

- [ ] **Step 3: Install test dependencies**

```bash
npm install -D vitest @testing-library/react @testing-library/jest-dom @testing-library/user-event jsdom msw
```

- [ ] **Step 4: Configure Vitest in `vite.config.ts`**

```typescript
// frontend/vite.config.ts
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: './src/test/setup.ts',
  },
});
```

- [ ] **Step 5: Create the test setup file**

```typescript
// frontend/src/test/setup.ts
import '@testing-library/jest-dom/vitest';
```

- [ ] **Step 6: Create the `.env` file with the backend base URL (spec §9)**

```
# frontend/.env
VITE_API_BASE_URL=http://localhost:8080/api/v1
```

- [ ] **Step 7: Add the test script to `package.json`**

```json
// frontend/package.json (add to "scripts")
"test": "vitest run"
```

- [ ] **Step 8: Verify the scaffold builds and the (default) test setup runs**

```bash
npm run build
npx vitest run --passWithNoTests
```

Expected: build succeeds; Vitest reports no tests found but exits `0`.

- [ ] **Step 9: Commit**

```bash
git add frontend/
git commit -m "Scaffold Vite + React + TypeScript front-end project"
```

---

## Task 1: Shared Error Types and the Axios Client (spec §5)

This is the foundation every feature's `api.ts` builds on — the `ETag`
capture/`If-Match` attachment and `ProblemDetails`→`AppError` normalization
described in spec §5 live here, once, not repeated per feature.

**Files:**
- Create: `frontend/src/shared/lib/errors.ts`
- Create: `frontend/src/shared/lib/etagStore.ts`
- Create: `frontend/src/shared/lib/axiosClient.ts`
- Test: `frontend/src/shared/lib/errors.test.ts`
- Test: `frontend/src/shared/lib/axiosClient.test.ts`

- [ ] **Step 1: Write the failing test for `AppError` normalization**

```typescript
// frontend/src/shared/lib/errors.test.ts
import { describe, expect, it } from 'vitest';
import { toAppError } from './errors';

describe('toAppError', () => {
  it('maps a ProblemDetails body with field errors into fieldErrors', () => {
    const axiosError = {
      response: {
        status: 400,
        data: {
          title: 'Validation failed.',
          status: 400,
          errors: [{ propertyName: 'Name', errorMessage: 'Name is required.' }],
        },
      },
    };

    const result = toAppError(axiosError);

    expect(result.status).toBe(400);
    expect(result.message).toBe('Validation failed.');
    expect(result.fieldErrors).toEqual([{ propertyName: 'Name', errorMessage: 'Name is required.' }]);
  });

  it('maps a 500 ProblemDetails body, surfacing the traceId', () => {
    const axiosError = {
      response: {
        status: 500,
        data: { title: 'An unexpected error occurred.', status: 500, traceId: 'abc-123' },
      },
    };

    const result = toAppError(axiosError);

    expect(result.status).toBe(500);
    expect(result.traceId).toBe('abc-123');
  });

  it('falls back to a generic message when there is no response at all (network failure)', () => {
    const axiosError = { response: undefined, message: 'Network Error' };

    const result = toAppError(axiosError);

    expect(result.status).toBe(0);
    expect(result.message).toBe('Network Error');
  });
});
```

- [ ] **Step 2: Run to verify failure**

```bash
npx vitest run errors.test.ts
```

Expected: FAIL — `errors.ts` doesn't exist yet.

- [ ] **Step 3: Implement `AppError` and `toAppError`**

```typescript
// frontend/src/shared/lib/errors.ts
export interface FieldError {
  propertyName: string;
  errorMessage: string;
}

export interface AppError {
  status: number;
  message: string;
  fieldErrors?: FieldError[];
  traceId?: string;
  /**
   * The raw response body, always preserved. Most endpoints return a
   * ProblemDetails-shaped error (title/errors/traceId, extracted into the
   * fields above) - but the stock-adjustment endpoint deliberately does
   * NOT (backend spec section 7: insufficient stock isn't an exception,
   * so its 409 body is the plain AdjustStockResult shape, not
   * ProblemDetails). Callers that know they're calling an endpoint with a
   * non-standard error body read it from here instead of the fields above.
   */
  raw?: unknown;
}

interface AxiosLikeError {
  response?: { status: number; data?: Record<string, unknown> };
  message?: string;
}

export function toAppError(error: AxiosLikeError): AppError {
  if (!error.response) {
    return { status: 0, message: error.message ?? 'Network error' };
  }

  const { status, data } = error.response;
  const title = (data?.title as string | undefined) ?? 'An error occurred.';
  const fieldErrors = data?.errors as FieldError[] | undefined;
  const traceId = data?.traceId as string | undefined;

  return { status, message: title, fieldErrors, traceId, raw: data };
}
```

- [ ] **Step 4: Run to verify it passes**

```bash
npx vitest run errors.test.ts
```

Expected: PASS, 3 tests.

- [ ] **Step 5: Write the `ETag` store — a tiny in-memory map keyed by resource URL, since RTK Query's own cache entry isn't reachable from inside an Axios interceptor**

```typescript
// frontend/src/shared/lib/etagStore.ts
const etags = new Map<string, string>();

export function setETag(url: string, etag: string | undefined): void {
  if (etag) etags.set(url, etag);
}

export function getETag(url: string): string | undefined {
  return etags.get(url);
}
```

- [ ] **Step 6: Write the failing test for the Axios client's interceptors**

```typescript
// frontend/src/shared/lib/axiosClient.test.ts
import { describe, expect, it, vi, beforeEach } from 'vitest';
import MockAdapter from 'axios-mock-adapter';
import { axiosClient } from './axiosClient';
import { setETag, getETag } from './etagStore';

describe('axiosClient interceptors', () => {
  let mock: MockAdapter;

  beforeEach(() => {
    mock = new MockAdapter(axiosClient);
  });

  it('captures the ETag response header for a GET and stores it by URL', async () => {
    mock.onGet('/products/1').reply(200, { id: 1 }, { etag: '"42"' });

    await axiosClient.get('/products/1');

    expect(getETag('/products/1')).toBe('"42"');
  });

  it('attaches If-Match from the stored ETag on a PUT to the same URL', async () => {
    setETag('/products/2', '"99"');
    mock.onPut('/products/2').reply((config) => {
      expect(config.headers?.['If-Match']).toBe('"99"');
      return [200, { id: 2 }];
    });

    await axiosClient.put('/products/2', { name: 'x' });
  });

  it('rejects with a normalized AppError on a 409 response', async () => {
    mock.onPut('/products/3').reply(409, { title: 'Conflict.', status: 409 });

    await expect(axiosClient.put('/products/3', {})).rejects.toMatchObject({ status: 409, message: 'Conflict.' });
  });
});
```

- [ ] **Step 7: Install the mock adapter dev dependency and run to verify failure**

```bash
npm install -D axios-mock-adapter
npx vitest run axiosClient.test.ts
```

Expected: FAIL — `axiosClient.ts` doesn't exist yet.

- [ ] **Step 8: Implement `axiosClient` with both interceptors**

```typescript
// frontend/src/shared/lib/axiosClient.ts
import axios from 'axios';
import { getETag, setETag } from './etagStore';
import { toAppError } from './errors';

export const axiosClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
});

axiosClient.interceptors.request.use((config) => {
  const method = config.method?.toLowerCase();
  if ((method === 'put' || method === 'patch') && config.url) {
    const etag = getETag(config.url);
    if (etag) {
      config.headers = config.headers ?? {};
      config.headers['If-Match'] = etag;
    }
  }
  return config;
});

axiosClient.interceptors.response.use(
  (response) => {
    const etag = response.headers?.etag as string | undefined;
    if (etag && response.config.url) setETag(response.config.url, etag);
    return response;
  },
  (error) => Promise.reject(toAppError(error)),
);
```

- [ ] **Step 9: Run to verify all pass**

```bash
npx vitest run errors.test.ts axiosClient.test.ts
```

Expected: PASS, 6 tests total.

- [ ] **Step 10: Commit**

```bash
git add frontend/src/shared/lib
git commit -m "Add AppError normalization and Axios client with ETag/If-Match interceptors"
```

---

## Task 2: Redux Store and the RTK Query Base API

**Files:**
- Create: `frontend/src/shared/lib/apiBase.ts`
- Create: `frontend/src/app/store.ts`
- Modify: `frontend/src/main.tsx`

- [ ] **Step 1: Write `apiBase.ts` — the shared `createApi` instance every feature's `api.ts` injects endpoints into**

```typescript
// frontend/src/shared/lib/apiBase.ts
import { createApi } from '@reduxjs/toolkit/query/react';
import type { BaseQueryFn } from '@reduxjs/toolkit/query';
import { AxiosError, AxiosRequestConfig } from 'axios';
import { axiosClient } from './axiosClient';
import { AppError } from './errors';

const axiosBaseQuery: BaseQueryFn<
  { url: string; method: AxiosRequestConfig['method']; data?: unknown; params?: unknown; headers?: Record<string, string> },
  unknown,
  AppError
> = async ({ url, method, data, params, headers }) => {
  try {
    const response = await axiosClient({ url, method, data, params, headers });
    return { data: response.data };
  } catch (err) {
    return { error: err as AppError };
  }
};

export const api = createApi({
  reducerPath: 'api',
  baseQuery: axiosBaseQuery,
  tagTypes: ['Product', 'ProductList', 'Category', 'CategoryList'],
  endpoints: () => ({}),
});
```

- [ ] **Step 2: Write the Redux store**

```typescript
// frontend/src/app/store.ts
import { configureStore } from '@reduxjs/toolkit';
import { api } from '../shared/lib/apiBase';

export const store = configureStore({
  reducer: { [api.reducerPath]: api.reducer },
  middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(api.middleware),
});

export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;
```

- [ ] **Step 3: Wire the store into the app**

```typescript
// frontend/src/main.tsx
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { Provider } from 'react-redux';
import App from './App';
import { store } from './app/store';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Provider store={store}>
      <App />
    </Provider>
  </StrictMode>,
);
```

- [ ] **Step 4: Verify the build still succeeds (no automated test for pure wiring — verified functionally once the first feature's endpoints exist in Task 4)**

```bash
npm run build
```

Expected: succeeds, no type errors.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/shared/lib/apiBase.ts frontend/src/app frontend/src/main.tsx
git commit -m "Add Redux store and RTK Query base API with Axios baseQuery"
```

---

## Task 3: App Shell — Sidebar Layout, Lazy Routes, Error Boundary (spec §6, §7, §8)

**Files:**
- Create: `frontend/src/shared/components/AppLayout.tsx`
- Create: `frontend/src/shared/components/ErrorBoundary.tsx`
- Create: `frontend/src/app/router.tsx`
- Modify: `frontend/src/App.tsx`
- Test: `frontend/src/shared/components/ErrorBoundary.test.tsx`

- [ ] **Step 1: Write the failing test for `ErrorBoundary` (spec §8 "Render-time crash containment")**

```typescript
// frontend/src/shared/components/ErrorBoundary.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ErrorBoundary } from './ErrorBoundary';

function Bomb(): never {
  throw new Error('boom');
}

describe('ErrorBoundary', () => {
  it('renders a fallback UI instead of crashing to a blank page', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {}); // React logs the caught error - silence it in the test

    render(
      <ErrorBoundary>
        <Bomb />
      </ErrorBoundary>,
    );

    expect(screen.getByText(/something went wrong/i)).toBeInTheDocument();
  });

  it('renders children normally when nothing throws', () => {
    render(
      <ErrorBoundary>
        <div>All good</div>
      </ErrorBoundary>,
    );

    expect(screen.getByText('All good')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run to verify failure**

```bash
npx vitest run ErrorBoundary.test.tsx
```

Expected: FAIL — `ErrorBoundary` doesn't exist yet.

- [ ] **Step 3: Implement `ErrorBoundary`**

```typescript
// frontend/src/shared/components/ErrorBoundary.tsx
import { Component, ReactNode } from 'react';
import { Result, Button } from 'antd';

interface Props {
  children: ReactNode;
}

interface State {
  hasError: boolean;
}

export class ErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false };

  static getDerivedStateFromError(): State {
    return { hasError: true };
  }

  componentDidCatch(error: Error): void {
    console.error('Unhandled render error:', error);
  }

  render() {
    if (this.state.hasError) {
      return (
        <Result
          status="error"
          title="Something went wrong"
          subTitle="Try reloading the page."
          extra={
            <Button type="primary" onClick={() => window.location.reload()}>
              Reload
            </Button>
          }
        />
      );
    }
    return this.props.children;
  }
}
```

- [ ] **Step 4: Run to verify it passes**

```bash
npx vitest run ErrorBoundary.test.tsx
```

Expected: PASS, 2 tests.

- [ ] **Step 5: Write `AppLayout` — the sidebar shell confirmed via the design review mockup (spec §6)**

```typescript
// frontend/src/shared/components/AppLayout.tsx
import { Layout, Menu } from 'antd';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';

const { Sider, Header, Content } = Layout;

const navItems = [
  { key: '/products', label: 'Products' },
  { key: '/categories', label: 'Categories' },
];

export function AppLayout() {
  const location = useLocation();
  const navigate = useNavigate();
  const selectedKey = navItems.find((item) => location.pathname.startsWith(item.key))?.key ?? '/products';

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Sider>
        <div style={{ color: 'white', padding: 16, fontWeight: 600 }}>Admin</div>
        <Menu
          theme="dark"
          mode="inline"
          selectedKeys={[selectedKey]}
          items={navItems}
          onClick={({ key }) => navigate(key)}
        />
      </Sider>
      <Layout>
        <Header style={{ background: '#fff', paddingLeft: 24 }} />
        <Content style={{ margin: 24 }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
}
```

- [ ] **Step 6: Write the router with lazy-loaded routes (spec §7 "Route-based code splitting")**

```typescript
// frontend/src/app/router.tsx
import { lazy, Suspense } from 'react';
import { createBrowserRouter } from 'react-router-dom';
import { Spin } from 'antd';
import { AppLayout } from '../shared/components/AppLayout';

const ProductListPage = lazy(() => import('../features/products/ProductListPage').then((m) => ({ default: m.ProductListPage })));
const ProductDetailPage = lazy(() => import('../features/products/ProductDetailPage').then((m) => ({ default: m.ProductDetailPage })));
const CategoryListPage = lazy(() => import('../features/categories/CategoryListPage').then((m) => ({ default: m.CategoryListPage })));

function withSuspense(element: React.ReactNode) {
  return <Suspense fallback={<Spin size="large" style={{ marginTop: 48 }} />}>{element}</Suspense>;
}

export const router = createBrowserRouter([
  {
    path: '/',
    element: <AppLayout />,
    children: [
      { index: true, element: withSuspense(<ProductListPage />) },
      { path: 'products', element: withSuspense(<ProductListPage />) },
      { path: 'products/new', element: withSuspense(<ProductDetailPage />) },
      { path: 'products/:id', element: withSuspense(<ProductDetailPage />) },
      { path: 'categories', element: withSuspense(<CategoryListPage />) },
    ],
  },
]);
```

- [ ] **Step 7: Wire `App.tsx`**

```typescript
// frontend/src/App.tsx
import { RouterProvider } from 'react-router-dom';
import { ConfigProvider } from 'antd';
import { router } from './app/router';
import { ErrorBoundary } from './shared/components/ErrorBoundary';

export default function App() {
  return (
    <ErrorBoundary>
      <ConfigProvider>
        <RouterProvider router={router} />
      </ConfigProvider>
    </ErrorBoundary>
  );
}
```

This references `ProductListPage`, `ProductDetailPage`, and `CategoryListPage`,
which don't exist until Tasks 5, 6, and 4 respectively — `npm run build`
won't succeed until those land. That's expected; this task establishes the
shell and routing shape, the next tasks fill in the pages it points to.

- [ ] **Step 8: Commit**

```bash
git add frontend/src/shared/components frontend/src/app/router.tsx frontend/src/App.tsx
git commit -m "Add app shell: sidebar layout, lazy-loaded routes, error boundary"
```

---

## Task 4: Categories Feature (tree table, MSW setup — spec §6, §9)

This is the first feature slice, and where MSW gets set up (spec §9's
"Frontend: `npm test` — needs nothing running; MSW mocks every API call")
— every later feature's tests reuse this same mock server, just adding
their own handlers.

**Files:**
- Create: `frontend/src/mocks/handlers.ts`
- Create: `frontend/src/mocks/server.ts`
- Modify: `frontend/src/test/setup.ts`
- Create: `frontend/src/features/categories/types.ts`
- Create: `frontend/src/features/categories/api.ts`
- Create: `frontend/src/features/categories/buildCategoryTree.ts`
- Create: `frontend/src/features/categories/CategoryForm.tsx`
- Create: `frontend/src/features/categories/CategoryListPage.tsx`
- Test: `frontend/src/features/categories/buildCategoryTree.test.ts`
- Test: `frontend/src/features/categories/CategoryListPage.test.tsx`

- [ ] **Step 1: Write the failing test for `buildCategoryTree` — pure logic, no network needed**

```typescript
// frontend/src/features/categories/buildCategoryTree.test.ts
import { describe, expect, it } from 'vitest';
import { buildCategoryTree } from './buildCategoryTree';
import { Category } from './types';

const women: Category = { id: 1, name: 'Women', slug: 'women', parentCategoryId: null, displayOrder: 0, isActive: true };
const dresses: Category = { id: 2, name: 'Dresses', slug: 'dresses', parentCategoryId: 1, displayOrder: 0, isActive: true };
const maxiDresses: Category = { id: 3, name: 'Maxi Dresses', slug: 'maxi-dresses', parentCategoryId: 2, displayOrder: 0, isActive: true };

describe('buildCategoryTree', () => {
  it('nests children under their parent, three levels deep', () => {
    const tree = buildCategoryTree([women, dresses, maxiDresses]);

    expect(tree).toHaveLength(1);
    expect(tree[0].id).toBe(1);
    expect(tree[0].children?.[0].id).toBe(2);
    expect(tree[0].children?.[0].children?.[0].id).toBe(3);
  });

  it('puts a category with a missing parent reference at the top level instead of dropping it', () => {
    const orphan: Category = { id: 4, name: 'Orphan', slug: 'orphan', parentCategoryId: 999, displayOrder: 0, isActive: true };

    const tree = buildCategoryTree([orphan]);

    expect(tree).toHaveLength(1);
    expect(tree[0].id).toBe(4);
  });
});
```

- [ ] **Step 2: Run to verify failure**

```bash
npx vitest run buildCategoryTree.test.ts
```

Expected: FAIL — nothing exists yet.

- [ ] **Step 3: Write `types.ts` and `buildCategoryTree.ts`**

```typescript
// frontend/src/features/categories/types.ts
export interface Category {
  id: number;
  name: string;
  slug: string;
  parentCategoryId: number | null;
  displayOrder: number;
  isActive: boolean;
}

export interface CreateCategoryRequest {
  name: string;
  slug: string;
  parentCategoryId: number | null;
  displayOrder: number;
}

export interface UpdateCategoryRequest extends CreateCategoryRequest {
  isActive: boolean;
}
```

```typescript
// frontend/src/features/categories/buildCategoryTree.ts
import { Category } from './types';

export interface CategoryTreeNode extends Category {
  children?: CategoryTreeNode[];
}

export function buildCategoryTree(categories: Category[]): CategoryTreeNode[] {
  const byId = new Map<number, CategoryTreeNode>(categories.map((c) => [c.id, { ...c }]));
  const roots: CategoryTreeNode[] = [];

  for (const category of byId.values()) {
    const parent = category.parentCategoryId !== null ? byId.get(category.parentCategoryId) : undefined;
    if (parent) {
      parent.children = parent.children ?? [];
      parent.children.push(category);
    } else {
      roots.push(category); // top-level, or an orphaned reference - shown rather than silently dropped
    }
  }

  return roots;
}
```

- [ ] **Step 4: Run to verify it passes**

```bash
npx vitest run buildCategoryTree.test.ts
```

Expected: PASS, 2 tests.

- [ ] **Step 5: Set up MSW — handlers, server, and wire it into the Vitest setup file**

```typescript
// frontend/src/mocks/handlers.ts
import { http, HttpResponse } from 'msw';
import type { Category } from '../features/categories/types';

const baseUrl = 'http://localhost:8080/api/v1';

export let mockCategories: Category[] = [
  { id: 1, name: 'Women', slug: 'women', parentCategoryId: null, displayOrder: 0, isActive: true },
  { id: 2, name: 'Dresses', slug: 'dresses', parentCategoryId: 1, displayOrder: 0, isActive: true },
];

export function resetMockCategories(): void {
  mockCategories = [
    { id: 1, name: 'Women', slug: 'women', parentCategoryId: null, displayOrder: 0, isActive: true },
    { id: 2, name: 'Dresses', slug: 'dresses', parentCategoryId: 1, displayOrder: 0, isActive: true },
  ];
}

export const handlers = [
  http.get(`${baseUrl}/categories`, () => HttpResponse.json(mockCategories)),

  http.post(`${baseUrl}/categories`, async ({ request }) => {
    const body = (await request.json()) as { name: string; slug: string };
    if (mockCategories.some((c) => c.slug === body.slug)) {
      return HttpResponse.json({ title: 'Slug already exists.', status: 409 }, { status: 409 });
    }
    const created: Category = { id: mockCategories.length + 1, ...body, parentCategoryId: null, displayOrder: 0, isActive: true } as Category;
    mockCategories.push(created);
    return HttpResponse.json(created, { status: 201 });
  }),

  http.delete(`${baseUrl}/categories/:id`, ({ params }) => {
    const id = Number(params.id);
    // Category 1 ("Women") is deliberately treated as "referenced by active products" in
    // these mocks, to exercise the 409 path (spec section 8) without needing a real products mock.
    if (id === 1) return HttpResponse.json({ title: 'Category has active products.', status: 409 }, { status: 409 });
    mockCategories = mockCategories.filter((c) => c.id !== id);
    return new HttpResponse(null, { status: 204 });
  }),
];
```

```typescript
// frontend/src/mocks/server.ts
import { setupServer } from 'msw/node';
import { handlers } from './handlers';

export const server = setupServer(...handlers);
```

```typescript
// frontend/src/test/setup.ts  (replace entire file)
import '@testing-library/jest-dom/vitest';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { server } from '../mocks/server';

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());
```

- [ ] **Step 6: Write `api.ts` — RTK Query endpoints for categories**

```typescript
// frontend/src/features/categories/api.ts
import { api } from '../../shared/lib/apiBase';
import { Category, CreateCategoryRequest, UpdateCategoryRequest } from './types';

export const categoriesApi = api.injectEndpoints({
  endpoints: (builder) => ({
    listCategories: builder.query<Category[], void>({
      query: () => ({ url: '/categories', method: 'GET' }),
      providesTags: ['CategoryList'],
    }),
    createCategory: builder.mutation<Category, CreateCategoryRequest>({
      query: (body) => ({ url: '/categories', method: 'POST', data: body }),
      invalidatesTags: ['CategoryList'],
    }),
    updateCategory: builder.mutation<Category, { id: number; body: UpdateCategoryRequest }>({
      query: ({ id, body }) => ({ url: `/categories/${id}`, method: 'PUT', data: body }),
      invalidatesTags: (_result, _error, { id }) => ['CategoryList', { type: 'Category', id }],
    }),
    deleteCategory: builder.mutation<void, number>({
      query: (id) => ({ url: `/categories/${id}`, method: 'DELETE' }),
      invalidatesTags: ['CategoryList'],
    }),
  }),
});

export const { useListCategoriesQuery, useCreateCategoryMutation, useUpdateCategoryMutation, useDeleteCategoryMutation } = categoriesApi;
```

- [ ] **Step 7: Write `CategoryForm`**

```typescript
// frontend/src/features/categories/CategoryForm.tsx
import { Form, Input, InputNumber, TreeSelect, Button, message } from 'antd';
import { useCreateCategoryMutation, useUpdateCategoryMutation } from './api';
import { Category } from './types';
import { buildCategoryTree, CategoryTreeNode } from './buildCategoryTree';
import { AppError } from '../../shared/lib/errors';

interface Props {
  category: Category | null;
  categories: Category[];
  onDone: () => void;
}

interface TreeSelectNode {
  title: string;
  value: number;
  children?: TreeSelectNode[];
}

function toTreeSelectNode(node: CategoryTreeNode): TreeSelectNode {
  return { title: node.name, value: node.id, children: node.children?.map(toTreeSelectNode) };
}

export function CategoryForm({ category, categories, onDone }: Props) {
  const [form] = Form.useForm();
  const [createCategory, { isLoading: creating }] = useCreateCategoryMutation();
  const [updateCategory, { isLoading: updating }] = useUpdateCategoryMutation();

  const treeData = buildCategoryTree(categories.filter((c) => c.id !== category?.id)).map(toTreeSelectNode);

  const handleFinish = async (values: { name: string; slug: string; parentCategoryId?: number; displayOrder: number }) => {
    try {
      const parentCategoryId = values.parentCategoryId ?? null;
      if (category) {
        await updateCategory({ id: category.id, body: { ...values, parentCategoryId, isActive: category.isActive } }).unwrap();
      } else {
        await createCategory({ ...values, parentCategoryId }).unwrap();
      }
      onDone();
    } catch (err) {
      const appError = err as AppError;
      if (appError.fieldErrors) {
        form.setFields(
          appError.fieldErrors.map((fe) => ({
            name: fe.propertyName.charAt(0).toLowerCase() + fe.propertyName.slice(1),
            errors: [fe.errorMessage],
          })),
        );
      } else {
        message.error(appError.message);
      }
    }
  };

  return (
    <Form form={form} layout="vertical" initialValues={category ?? { displayOrder: 0 }} onFinish={handleFinish}>
      <Form.Item name="name" label="Name" rules={[{ required: true, max: 120 }]}>
        <Input />
      </Form.Item>
      <Form.Item
        name="slug"
        label="Slug"
        rules={[{ required: true, pattern: /^[a-z0-9-]+$/, message: 'Lowercase letters, numbers, and hyphens only.' }]}
      >
        <Input />
      </Form.Item>
      <Form.Item name="parentCategoryId" label="Parent Category">
        <TreeSelect treeData={treeData} allowClear placeholder="None (top-level)" />
      </Form.Item>
      <Form.Item name="displayOrder" label="Display Order">
        <InputNumber style={{ width: '100%' }} />
      </Form.Item>
      <Button type="primary" htmlType="submit" loading={creating || updating}>
        Save
      </Button>
    </Form>
  );
}
```

- [ ] **Step 8: Write `CategoryListPage`**

```typescript
// frontend/src/features/categories/CategoryListPage.tsx
import { useState } from 'react';
import { Table, Button, Modal, Popconfirm, message } from 'antd';
import { useListCategoriesQuery, useDeleteCategoryMutation } from './api';
import { buildCategoryTree } from './buildCategoryTree';
import { CategoryForm } from './CategoryForm';
import { Category } from './types';
import { AppError } from '../../shared/lib/errors';

export function CategoryListPage() {
  const { data: categories, isLoading } = useListCategoriesQuery();
  const [deleteCategory] = useDeleteCategoryMutation();
  const [formOpen, setFormOpen] = useState(false);
  const [editingCategory, setEditingCategory] = useState<Category | null>(null);

  const treeData = buildCategoryTree(categories ?? []);

  const handleDelete = async (id: number) => {
    try {
      await deleteCategory(id).unwrap();
    } catch (err) {
      const appError = err as AppError;
      message.error(
        appError.status === 409
          ? 'Cannot delete: this category still has active products referencing it.'
          : appError.message,
      );
    }
  };

  const columns = [
    { title: 'Name', dataIndex: 'name', key: 'name' },
    { title: 'Slug', dataIndex: 'slug', key: 'slug' },
    { title: 'Active', dataIndex: 'isActive', key: 'isActive', render: (v: boolean) => (v ? 'Yes' : 'No') },
    {
      title: '',
      key: 'actions',
      render: (_: unknown, record: Category) => (
        <>
          <Button
            type="link"
            onClick={() => {
              setEditingCategory(record);
              setFormOpen(true);
            }}
          >
            Edit
          </Button>
          <Popconfirm title="Delete this category?" onConfirm={() => handleDelete(record.id)}>
            <Button type="link" danger>
              Delete
            </Button>
          </Popconfirm>
        </>
      ),
    },
  ];

  return (
    <div>
      <Button
        type="primary"
        onClick={() => {
          setEditingCategory(null);
          setFormOpen(true);
        }}
        style={{ marginBottom: 16 }}
      >
        + New Category
      </Button>
      <Table rowKey="id" columns={columns} dataSource={treeData} loading={isLoading} pagination={false} />
      <Modal open={formOpen} onCancel={() => setFormOpen(false)} footer={null} title={editingCategory ? 'Edit Category' : 'New Category'} destroyOnClose>
        <CategoryForm category={editingCategory} categories={categories ?? []} onDone={() => setFormOpen(false)} />
      </Modal>
    </div>
  );
}
```

- [ ] **Step 9: Write the failing `CategoryListPage` integration test — renders the tree, and exercises the 409 delete path against MSW**

```typescript
// frontend/src/features/categories/CategoryListPage.test.tsx
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import { describe, expect, it } from 'vitest';
import { api } from '../../shared/lib/apiBase';
import { CategoryListPage } from './CategoryListPage';
import { resetMockCategories } from '../../mocks/handlers';

function renderWithStore() {
  const store = configureStore({
    reducer: { [api.reducerPath]: api.reducer },
    middleware: (getDefault) => getDefault().concat(api.middleware),
  });
  return render(
    <Provider store={store}>
      <CategoryListPage />
    </Provider>,
  );
}

describe('CategoryListPage', () => {
  it('renders categories nested under their parent (tree table)', async () => {
    resetMockCategories();
    renderWithStore();

    expect(await screen.findByText('Women')).toBeInTheDocument();
    expect(screen.getByText('Dresses')).toBeInTheDocument();
  });

  it('shows a specific message when deleting a category blocked by active products (409)', async () => {
    resetMockCategories();
    renderWithStore();
    await screen.findByText('Women');

    const womenRow = screen.getByText('Women').closest('tr')!;
    await userEvent.click(within(womenRow).getByText('Delete'));
    await userEvent.click(await screen.findByRole('button', { name: /ok|yes/i }));

    expect(await screen.findByText(/still has active products/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 10: Run to verify failure, then confirm they pass**

```bash
npx vitest run buildCategoryTree.test.ts CategoryListPage.test.tsx
```

Expected: PASS, 4 tests total (2 from Step 4, 2 new here).

- [ ] **Step 11: Commit**

```bash
git add frontend/src/mocks frontend/src/test/setup.ts frontend/src/features/categories
git commit -m "Implement Categories feature: tree table, form, MSW mock server"
```

---

## Task 5: Products Feature — List Page (data table, search, cursor pagination — spec §5, §6, §7, §8)

**Files:**
- Create: `frontend/src/features/products/types.ts`
- Create: `frontend/src/features/products/api.ts`
- Create: `frontend/src/shared/hooks/useDebouncedValue.ts`
- Create: `frontend/src/features/products/ProductListPage.tsx`
- Modify: `frontend/src/mocks/handlers.ts`
- Test: `frontend/src/shared/hooks/useDebouncedValue.test.ts`
- Test: `frontend/src/features/products/ProductListPage.test.tsx`

- [ ] **Step 1: Write the failing test for `useDebouncedValue`**

```typescript
// frontend/src/shared/hooks/useDebouncedValue.test.ts
import { renderHook, act } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { useDebouncedValue } from './useDebouncedValue';

describe('useDebouncedValue', () => {
  it('only updates after the delay elapses', () => {
    vi.useFakeTimers();
    const { result, rerender } = renderHook(({ value }) => useDebouncedValue(value, 350), {
      initialProps: { value: 'a' },
    });

    rerender({ value: 'ab' });
    expect(result.current).toBe('a'); // not yet - delay hasn't elapsed

    act(() => vi.advanceTimersByTime(350));
    expect(result.current).toBe('ab');

    vi.useRealTimers();
  });
});
```

- [ ] **Step 2: Run to verify failure, then implement**

```bash
npx vitest run useDebouncedValue.test.ts
```

Expected: FAIL — doesn't exist yet.

```typescript
// frontend/src/shared/hooks/useDebouncedValue.ts
import { useEffect, useState } from 'react';

export function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);

  return debounced;
}
```

```bash
npx vitest run useDebouncedValue.test.ts
```

Expected: PASS, 1 test.

- [ ] **Step 3: Write `types.ts`, matching the backend's `ProductDto`/`ProductListItemDto`/`PagedResult<T>` shapes exactly**

```typescript
// frontend/src/features/products/types.ts
export interface Variant {
  id: number;
  sku: string;
  size: string | null;
  color: string | null;
  price: number;
  compareAtPrice: number | null;
  stockQuantity: number;
  barcode: string | null;
  isActive: boolean;
}

export interface Product {
  id: number;
  name: string;
  slug: string;
  description: string | null;
  categoryId: number;
  brand: string | null;
  status: string;
  attributes: string;
  imageUrl: string | null;
  variants: Variant[];
}

export interface ProductListItem {
  id: number;
  name: string;
  slug: string;
  categoryId: number;
  brand: string | null;
  status: string;
  minPrice: number | null;
  maxPrice: number | null;
  totalStock: number;
  imageUrl: string | null;
}

export interface PagedResult<T> {
  items: T[];
  nextCursor: string | null;
  hasMore: boolean;
}

export interface CreateVariantRequest {
  sku: string;
  size: string | null;
  color: string | null;
  price: number;
  stockQuantity: number;
  compareAtPrice: number | null;
  barcode: string | null;
}

export interface CreateProductRequest {
  name: string;
  slug: string;
  categoryId: number;
  brand: string | null;
  description: string | null;
  attributes: string;
  variants: CreateVariantRequest[];
}

export interface UpdateProductRequest {
  name: string;
  description: string | null;
  categoryId: number;
  brand: string | null;
  attributes: string;
}
```

- [ ] **Step 4: Write `api.ts`**

```typescript
// frontend/src/features/products/api.ts
import { api } from '../../shared/lib/apiBase';
import { Product, ProductListItem, PagedResult, CreateProductRequest, UpdateProductRequest } from './types';

export interface ListProductsParams {
  categoryId?: number;
  status?: number;
  q?: string;
  minPrice?: number;
  maxPrice?: number;
  cursor?: string;
  limit?: number;
}

export const productsApi = api.injectEndpoints({
  endpoints: (builder) => ({
    listProducts: builder.query<PagedResult<ProductListItem>, ListProductsParams>({
      query: (params) => ({ url: '/products', method: 'GET', params }),
      providesTags: ['ProductList'],
    }),
    getProduct: builder.query<Product, number>({
      query: (id) => ({ url: `/products/${id}`, method: 'GET' }),
      providesTags: (_result, _error, id) => [{ type: 'Product', id }],
    }),
    createProduct: builder.mutation<Product, CreateProductRequest>({
      query: (body) => ({ url: '/products', method: 'POST', data: body }),
      invalidatesTags: ['ProductList'],
    }),
    updateProduct: builder.mutation<Product, { id: number; body: UpdateProductRequest }>({
      query: ({ id, body }) => ({ url: `/products/${id}`, method: 'PUT', data: body }),
      invalidatesTags: (_result, _error, { id }) => ['ProductList', { type: 'Product', id }],
    }),
    deleteProduct: builder.mutation<void, number>({
      query: (id) => ({ url: `/products/${id}`, method: 'DELETE' }),
      invalidatesTags: ['ProductList'],
    }),
  }),
});

export const {
  useListProductsQuery,
  useGetProductQuery,
  useCreateProductMutation,
  useUpdateProductMutation,
  useDeleteProductMutation,
} = productsApi;
```

- [ ] **Step 5: Add product handlers to the MSW mock server**

```typescript
// frontend/src/mocks/handlers.ts  (add these to the existing `handlers` array, and export mockProducts)
import type { Product, ProductListItem } from '../features/products/types';

function initialMockProducts(): Product[] {
  return [
    {
      id: 1, name: 'Classic Cotton Tee', slug: 'classic-cotton-tee', description: null,
      categoryId: 2, brand: 'Acme', status: 'Active', attributes: '{}', imageUrl: null,
      variants: [{ id: 1, sku: 'TEE-M', size: 'M', color: 'Blue', price: 20, compareAtPrice: null, stockQuantity: 50, barcode: null, isActive: true }],
    },
  ];
}

export let mockProducts: Product[] = initialMockProducts();

// Mirrors resetMockCategories (Task 4) - every test file whose handlers mutate
// mockProducts (create/update/stock-adjust/image/delete) must call this before
// each test, or state leaks across tests within the same Vitest run.
export function resetMockProducts(): void {
  mockProducts = initialMockProducts();
}

// Append to the `handlers` array from Step 5 of Task 4:
http.get(`${baseUrl}/products`, ({ request }) => {
  const url = new URL(request.url);
  const q = url.searchParams.get('q');
  const filtered = q ? mockProducts.filter((p) => p.name.toLowerCase().includes(q.toLowerCase())) : mockProducts;
  const items: ProductListItem[] = filtered.map((p) => ({
    id: p.id, name: p.name, slug: p.slug, categoryId: p.categoryId, brand: p.brand, status: p.status,
    minPrice: p.variants[0]?.price ?? null, maxPrice: p.variants[0]?.price ?? null,
    totalStock: p.variants.reduce((sum, v) => sum + v.stockQuantity, 0), imageUrl: p.imageUrl,
  }));
  return HttpResponse.json({ items, nextCursor: null, hasMore: false });
}),

http.get(`${baseUrl}/products/:id`, ({ params }) => {
  const product = mockProducts.find((p) => p.id === Number(params.id));
  if (!product) return new HttpResponse(null, { status: 404 });
  return HttpResponse.json(product, { headers: { ETag: '"1"' } });
}),
```

(This step edits the existing `handlers.ts` from Task 4 — add the `import`,
the `mockProducts` export, and append the two new `http.get(...)` entries
into the same `handlers` array, alongside the categories ones.)

- [ ] **Step 6: Write `ProductListPage`, with URL-synced filters/cursor (spec §8) and debounced search (spec §7)**

```typescript
// frontend/src/features/products/ProductListPage.tsx
import { useState } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { Table, Input, Select, Button, Empty, Result } from 'antd';
import { useListProductsQuery } from './api';
import { productsApi } from './api';
import { useListCategoriesQuery } from '../categories/api';
import { useDebouncedValue } from '../../shared/hooks/useDebouncedValue';
import { ProductListItem } from './types';

export function ProductListPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();
  const { data: categories } = useListCategoriesQuery();
  const prefetchProduct = productsApi.usePrefetch('getProduct');

  const q = searchParams.get('q') ?? '';
  const categoryId = searchParams.get('categoryId') ? Number(searchParams.get('categoryId')) : undefined;
  const cursor = searchParams.get('cursor') ?? undefined;
  const [cursorStack, setCursorStack] = useState<string[]>([]);

  const debouncedQ = useDebouncedValue(q, 350);

  const { data, isLoading, isFetching, isError, refetch } = useListProductsQuery({
    q: debouncedQ || undefined,
    categoryId,
    cursor,
    limit: 20,
  });

  function updateParam(key: string, value: string | undefined) {
    const next = new URLSearchParams(searchParams);
    if (value) next.set(key, value);
    else next.delete(key);
    next.delete('cursor'); // any filter change restarts pagination from page one
    setCursorStack([]);
    setSearchParams(next);
  }

  function handleNext() {
    if (!data?.nextCursor) return;
    setCursorStack((stack) => [...stack, cursor ?? '']);
    const next = new URLSearchParams(searchParams);
    next.set('cursor', data.nextCursor);
    setSearchParams(next);
  }

  function handlePrevious() {
    const stack = [...cursorStack];
    const previous = stack.pop();
    setCursorStack(stack);
    const next = new URLSearchParams(searchParams);
    if (previous) next.set('cursor', previous);
    else next.delete('cursor');
    setSearchParams(next);
  }

  const columns = [
    {
      title: 'Image',
      dataIndex: 'imageUrl',
      key: 'imageUrl',
      render: (url: string | null) =>
        url ? (
          <img
            src={url}
            alt=""
            loading="lazy"
            style={{ width: 40, height: 40, objectFit: 'cover' }}
            onError={(e) => {
              (e.target as HTMLImageElement).style.visibility = 'hidden';
            }}
          />
        ) : (
          '—'
        ),
    },
    { title: 'Name', dataIndex: 'name', key: 'name' },
    {
      title: 'Price',
      key: 'price',
      render: (_: unknown, r: ProductListItem) =>
        r.minPrice === null ? '—' : r.minPrice === r.maxPrice ? `$${r.minPrice}` : `$${r.minPrice}–$${r.maxPrice}`,
    },
    { title: 'Stock', dataIndex: 'totalStock', key: 'totalStock' },
    { title: 'Status', dataIndex: 'status', key: 'status' },
    {
      title: '',
      key: 'actions',
      render: (_: unknown, r: ProductListItem) => (
        <Button type="link" onClick={() => navigate(`/products/${r.id}`)}>
          Edit
        </Button>
      ),
    },
  ];

  // Network failure / API unreachable (spec section 8): a retry-able error state,
  // never an infinite spinner or a silently blank table.
  if (isError) {
    return (
      <Result
        status="error"
        title="Couldn't load products"
        subTitle="The API may be unreachable. Check your connection and try again."
        extra={
          <Button type="primary" onClick={() => refetch()}>
            Retry
          </Button>
        }
      />
    );
  }

  return (
    <div>
      <div style={{ display: 'flex', gap: 8, marginBottom: 16 }}>
        <Input.Search
          placeholder="Search products..."
          defaultValue={q}
          onChange={(e) => updateParam('q', e.target.value)}
          style={{ flex: 1 }}
          allowClear
        />
        <Select
          placeholder="Category"
          allowClear
          style={{ width: 200 }}
          value={categoryId}
          onChange={(v) => updateParam('categoryId', v?.toString())}
          options={categories?.map((c) => ({ label: c.name, value: c.id }))}
        />
        <Button type="primary" onClick={() => navigate('/products/new')}>
          + New Product
        </Button>
      </div>
      <Table
        rowKey="id"
        columns={columns}
        dataSource={data?.items ?? []}
        loading={isLoading || isFetching}
        pagination={false}
        locale={{ emptyText: <Empty description="No products match these filters." /> }}
        onRow={(record: ProductListItem) => ({
          // Prefetch on hover (spec section 7) - clicking the existing Edit button
          // to navigate is often instant since the data's already cached by then.
          onMouseEnter: () => prefetchProduct(record.id),
        })}
      />
      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 16 }}>
        <Button disabled={cursorStack.length === 0} onClick={handlePrevious}>
          Previous
        </Button>
        <Button disabled={!data?.hasMore} onClick={handleNext}>
          Next
        </Button>
      </div>
    </div>
  );
}
```

- [ ] **Step 7: Write the failing `ProductListPage` tests**

```typescript
// frontend/src/features/products/ProductListPage.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import { MemoryRouter } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { api } from '../../shared/lib/apiBase';
import { server } from '../../mocks/server';
import { resetMockProducts } from '../../mocks/handlers';
import { ProductListPage } from './ProductListPage';

function renderPage() {
  const store = configureStore({
    reducer: { [api.reducerPath]: api.reducer },
    middleware: (getDefault) => getDefault().concat(api.middleware),
  });
  return render(
    <Provider store={store}>
      <MemoryRouter>
        <ProductListPage />
      </MemoryRouter>
    </Provider>,
  );
}

describe('ProductListPage', () => {
  it('renders the seeded mock product', async () => {
    resetMockProducts();
    renderPage();

    expect(await screen.findByText('Classic Cotton Tee')).toBeInTheDocument();
  });

  it('shows the empty state when a search matches nothing', async () => {
    resetMockProducts();
    renderPage();
    await screen.findByText('Classic Cotton Tee');

    await userEvent.type(screen.getByPlaceholderText('Search products...'), 'nonexistent-product-xyz');

    expect(await screen.findByText(/no products match these filters/i)).toBeInTheDocument();
  });

  it('shows a retry-able error state, not a blank table, when the API is unreachable', async () => {
    server.use(http.get('http://localhost:8080/api/v1/products', () => HttpResponse.error()));
    renderPage();

    expect(await screen.findByText(/couldn't load products/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument();
  });
});
```

- [ ] **Step 8: Run to verify failure, then confirm they pass**

```bash
npx vitest run ProductListPage.test.tsx
```

Expected: PASS, 3 tests. (The empty-state test relies on the 350ms
debounce from Step 2 actually elapsing under Testing Library's real
timers — `findByText`'s default polling window comfortably covers that,
no fake-timer setup needed here unlike the hook's own unit test.)

- [ ] **Step 9: Commit**

```bash
git add frontend/src/features/products frontend/src/shared/hooks frontend/src/mocks/handlers.ts
git commit -m "Implement Products list page: data table, debounced search, cursor pagination"
```

---

## Task 6: Product Detail Page and Form — Single Stacked Page (spec §6)

Create (`/products/new`) and edit (`/products/:id`) share this one page,
per the confirmed design: core fields at the top, variants/image only
shown once a product actually exists (create mode saves the core fields
first, then redirects into edit mode where variants/image become
available — simpler than embedding a whole variant sub-form in the create
flow).

**Files:**
- Create: `frontend/src/features/products/ProductForm.tsx`
- Create: `frontend/src/features/products/ProductDetailPage.tsx`
- Modify: `frontend/src/mocks/handlers.ts`
- Test: `frontend/src/features/products/ProductDetailPage.test.tsx`

- [ ] **Step 1: Add the remaining product mock handlers (POST/PUT) needed for form submission tests**

```typescript
// frontend/src/mocks/handlers.ts  (append to the handlers array from Task 5)
http.post(`${baseUrl}/products`, async ({ request }) => {
  const body = (await request.json()) as { name: string; slug: string; categoryId: number; brand: string | null; description: string | null };
  const created: Product = {
    id: mockProducts.length + 1, ...body, attributes: '{}', imageUrl: null, status: 'Draft', variants: [],
  };
  mockProducts.push(created);
  return HttpResponse.json(created, { status: 201, headers: { Location: `/products/${created.id}` } });
}),

http.put(`${baseUrl}/products/:id`, async ({ params, request }) => {
  const product = mockProducts.find((p) => p.id === Number(params.id));
  if (!product) return new HttpResponse(null, { status: 404 });
  const ifMatch = request.headers.get('If-Match');
  if (ifMatch && ifMatch !== '"1"') {
    return HttpResponse.json({ title: 'Concurrency conflict.', status: 409 }, { status: 409 });
  }
  const body = (await request.json()) as { name: string; description: string | null; categoryId: number; brand: string | null };
  Object.assign(product, body);
  return HttpResponse.json(product, { headers: { ETag: '"1"' } });
}),
```

- [ ] **Step 2: Write `ProductForm`**

```typescript
// frontend/src/features/products/ProductForm.tsx
import { Form, Input, Select, Button, message } from 'antd';
import { useNavigate } from 'react-router-dom';
import { useCreateProductMutation, useUpdateProductMutation } from './api';
import { useListCategoriesQuery } from '../categories/api';
import { Product } from './types';
import { AppError } from '../../shared/lib/errors';

interface Props {
  product: Product | null;
}

export function ProductForm({ product }: Props) {
  const [form] = Form.useForm();
  const navigate = useNavigate();
  const { data: categories } = useListCategoriesQuery();
  const [createProduct, { isLoading: creating }] = useCreateProductMutation();
  const [updateProduct, { isLoading: updating }] = useUpdateProductMutation();

  const handleFinish = async (values: { name: string; slug: string; categoryId: number; brand?: string; description?: string }) => {
    try {
      if (product) {
        await updateProduct({
          id: product.id,
          body: {
            name: values.name,
            description: values.description ?? null,
            categoryId: values.categoryId,
            brand: values.brand ?? null,
            attributes: product.attributes,
          },
        }).unwrap();
        message.success('Product updated.');
      } else {
        const created = await createProduct({
          name: values.name,
          slug: values.slug,
          categoryId: values.categoryId,
          brand: values.brand ?? null,
          description: values.description ?? null,
          attributes: '{}',
          variants: [],
        }).unwrap();
        message.success('Product created.');
        navigate(`/products/${created.id}`);
      }
    } catch (err) {
      const appError = err as AppError;
      if (appError.status === 409) {
        // Full dedicated conflict UX (Reload latest action) added in Task 9 - basic
        // feedback here for now so the form doesn't fail silently in the meantime.
        message.error('This product was changed by someone else. Reload to see the latest version.');
      } else if (appError.fieldErrors) {
        form.setFields(
          appError.fieldErrors.map((fe) => ({
            name: fe.propertyName.charAt(0).toLowerCase() + fe.propertyName.slice(1),
            errors: [fe.errorMessage],
          })),
        );
      } else {
        message.error(appError.message);
      }
    }
  };

  return (
    <Form
      form={form}
      layout="vertical"
      initialValues={
        product
          ? { name: product.name, slug: product.slug, categoryId: product.categoryId, brand: product.brand ?? undefined, description: product.description ?? undefined }
          : {}
      }
      onFinish={handleFinish}
    >
      <Form.Item name="name" label="Name" rules={[{ required: true, max: 200 }]}>
        <Input />
      </Form.Item>
      {!product && (
        <Form.Item name="slug" label="Slug" rules={[{ required: true, pattern: /^[a-z0-9-]+$/, message: 'Lowercase letters, numbers, and hyphens only.' }]}>
          <Input />
        </Form.Item>
      )}
      <Form.Item name="categoryId" label="Category" rules={[{ required: true }]}>
        <Select options={categories?.map((c) => ({ label: c.name, value: c.id }))} />
      </Form.Item>
      <Form.Item name="brand" label="Brand">
        <Input />
      </Form.Item>
      <Form.Item name="description" label="Description">
        <Input.TextArea rows={3} />
      </Form.Item>
      <Button type="primary" htmlType="submit" loading={creating || updating}>
        Save
      </Button>
    </Form>
  );
}
```

- [ ] **Step 3: Write `ProductDetailPage`**

Variants/image aren't wired in yet — `VariantsTable`/`ImageUploader` are
imported as placeholders here and implemented for real in Tasks 7 and 8;
this task's own test only exercises the form.

```typescript
// frontend/src/features/products/ProductDetailPage.tsx
import { useParams } from 'react-router-dom';
import { Spin, Typography } from 'antd';
import { useGetProductQuery } from './api';
import { ProductForm } from './ProductForm';

export function ProductDetailPage() {
  const { id } = useParams<{ id: string }>();
  const isCreate = !id;
  const productId = id ? Number(id) : undefined;

  const { data: product, isLoading } = useGetProductQuery(productId!, { skip: isCreate });

  if (!isCreate && isLoading) return <Spin size="large" />;

  return (
    <div>
      <Typography.Title level={3}>{isCreate ? 'New Product' : product?.name}</Typography.Title>
      <ProductForm product={product ?? null} />
    </div>
  );
}
```

- [ ] **Step 4: Write the failing tests**

```typescript
// frontend/src/features/products/ProductDetailPage.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { api } from '../../shared/lib/apiBase';
import { resetMockProducts, resetMockCategories } from '../../mocks/handlers';
import { ProductDetailPage } from './ProductDetailPage';

function renderAt(path: string) {
  const store = configureStore({
    reducer: { [api.reducerPath]: api.reducer },
    middleware: (getDefault) => getDefault().concat(api.middleware),
  });
  return render(
    <Provider store={store}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/products/new" element={<ProductDetailPage />} />
          <Route path="/products/:id" element={<ProductDetailPage />} />
        </Routes>
      </MemoryRouter>
    </Provider>,
  );
}

describe('ProductDetailPage', () => {
  it('create mode shows an empty form with a Slug field', async () => {
    resetMockProducts();
    renderAt('/products/new');

    expect(await screen.findByText('New Product')).toBeInTheDocument();
    expect(screen.getByLabelText('Slug')).toBeInTheDocument();
  });

  it('edit mode loads and displays the existing product name, without a Slug field', async () => {
    resetMockProducts();
    renderAt('/products/1');

    expect(await screen.findByText('Classic Cotton Tee')).toBeInTheDocument();
    expect(screen.queryByLabelText('Slug')).not.toBeInTheDocument();
  });

  it('submitting valid create-mode data creates the product and navigates to its detail page', async () => {
    resetMockProducts();
    resetMockCategories();
    renderAt('/products/new');
    await screen.findByText('New Product');

    await userEvent.type(screen.getByLabelText('Name'), 'New Jacket');
    await userEvent.type(screen.getByLabelText('Slug'), 'new-jacket');
    await userEvent.click(screen.getByLabelText('Category'));
    await userEvent.click(await screen.findByText('Women'));
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    // Successful create navigates to /products/2 (the seeded product is id 1), which
    // re-renders this same component in edit mode showing the just-created product.
    expect(await screen.findByText('New Jacket')).toBeInTheDocument();
  });
});
```

- [ ] **Step 5: Run to verify failure, then confirm they pass**

```bash
npx vitest run ProductDetailPage.test.tsx
```

Expected: PASS, 3 tests.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/features/products frontend/src/mocks/handlers.ts
git commit -m "Implement Product detail page and form (create + edit modes)"
```

---

## Task 7: Variants Table — Optimistic Stock Adjustment (spec §5, §6, §8)

**Files:**
- Modify: `frontend/src/features/products/types.ts`
- Modify: `frontend/src/features/products/api.ts`
- Create: `frontend/src/features/products/VariantsTable.tsx`
- Modify: `frontend/src/features/products/ProductDetailPage.tsx`
- Modify: `frontend/src/mocks/handlers.ts`
- Test: `frontend/src/features/products/VariantsTable.test.tsx`

- [ ] **Step 1: Add `AdjustStockResult` to `types.ts`**

```typescript
// frontend/src/features/products/types.ts  (append)
export interface AdjustStockResult {
  succeeded: boolean;
  newQuantity: number | null;
  availableQuantity: number | null;
}
```

- [ ] **Step 2: Add variant/stock endpoints to `api.ts` — the optimistic update is the key piece here (spec §5)**

```typescript
// frontend/src/features/products/api.ts  (add inside the existing `injectEndpoints` call, alongside the endpoints from Task 5)
    createVariant: builder.mutation<Variant, { productId: number; body: CreateVariantRequest }>({
      query: ({ productId, body }) => ({ url: `/products/${productId}/variants`, method: 'POST', data: body }),
      invalidatesTags: (_result, _error, { productId }) => [{ type: 'Product', id: productId }],
    }),
    deleteVariant: builder.mutation<void, { productId: number; variantId: number }>({
      query: ({ productId, variantId }) => ({ url: `/products/${productId}/variants/${variantId}`, method: 'DELETE' }),
      invalidatesTags: (_result, _error, { productId }) => [{ type: 'Product', id: productId }],
    }),
    adjustStock: builder.mutation<AdjustStockResult, { productId: number; variantId: number; delta: number }>({
      query: ({ productId, variantId, delta }) => ({
        url: `/products/${productId}/variants/${variantId}/stock`,
        method: 'PATCH',
        data: { delta },
        headers: { 'Idempotency-Key': crypto.randomUUID() },
      }),
      async onQueryStarted({ productId, variantId, delta }, { dispatch, queryFulfilled }) {
        // Instant UI feedback (spec section 5) - the cached stock number updates before
        // the network round-trip completes, then rolls back automatically on failure.
        const patch = dispatch(
          productsApi.util.updateQueryData('getProduct', productId, (draft) => {
            const variant = draft.variants.find((v) => v.id === variantId);
            if (variant) variant.stockQuantity += delta;
          }),
        );
        try {
          await queryFulfilled;
        } catch {
          patch.undo();
        }
      },
    }),
```

Also add `AdjustStockResult` to the `types.ts` import line at the top of
`api.ts`, and add `useCreateVariantMutation`, `useDeleteVariantMutation`,
`useAdjustStockMutation` to the destructured export at the bottom of the
file (same pattern as the existing exports from Task 5).

- [ ] **Step 3: Add MSW handlers for variant creation and stock adjustment**

```typescript
// frontend/src/mocks/handlers.ts  (append)
http.post(`${baseUrl}/products/:productId/variants`, async ({ params, request }) => {
  const product = mockProducts.find((p) => p.id === Number(params.productId));
  if (!product) return new HttpResponse(null, { status: 404 });
  const body = (await request.json()) as { sku: string; size: string | null; color: string | null; price: number; stockQuantity: number };
  if (product.variants.some((v) => v.sku === body.sku)) {
    return HttpResponse.json({ title: 'SKU already exists.', status: 409 }, { status: 409 });
  }
  const created = { id: product.variants.length + 100, compareAtPrice: null, barcode: null, isActive: true, ...body };
  product.variants.push(created);
  return HttpResponse.json(created, { status: 201 });
}),

http.patch(`${baseUrl}/products/:productId/variants/:variantId/stock`, async ({ params, request }) => {
  const product = mockProducts.find((p) => p.id === Number(params.productId));
  const variant = product?.variants.find((v) => v.id === Number(params.variantId));
  if (!variant) return new HttpResponse(null, { status: 404 });
  const { delta } = (await request.json()) as { delta: number };

  if (delta < 0 && variant.stockQuantity + delta < 0) {
    return HttpResponse.json(
      { succeeded: false, newQuantity: null, availableQuantity: variant.stockQuantity },
      { status: 409 },
    );
  }
  variant.stockQuantity += delta;
  return HttpResponse.json({ succeeded: true, newQuantity: variant.stockQuantity, availableQuantity: null });
}),
```

- [ ] **Step 4: Write `VariantsTable`**

```typescript
// frontend/src/features/products/VariantsTable.tsx
import { memo } from 'react';
import { Table, Button, Space, message } from 'antd';
import { useAdjustStockMutation, useDeleteVariantMutation } from './api';
import { Variant } from './types';
import { AppError } from '../../shared/lib/errors';

interface Props {
  productId: number;
  variants: Variant[];
}

export const VariantsTable = memo(function VariantsTable({ productId, variants }: Props) {
  const [adjustStock] = useAdjustStockMutation();
  const [deleteVariant] = useDeleteVariantMutation();

  async function handleAdjust(variantId: number, delta: number) {
    try {
      await adjustStock({ productId, variantId, delta }).unwrap();
    } catch (err) {
      const appError = err as AppError;
      // The stock endpoint's 409 body is the raw AdjustStockResult shape, not
      // ProblemDetails - read availableQuantity from `raw`, not the standard fields.
      const raw = appError.raw as { availableQuantity?: number } | undefined;
      message.error(
        raw?.availableQuantity !== undefined
          ? `Only ${raw.availableQuantity} left — you requested more than that.`
          : appError.message,
      );
    }
  }

  const columns = [
    { title: 'SKU', dataIndex: 'sku', key: 'sku' },
    { title: 'Size/Color', key: 'variant', render: (_: unknown, v: Variant) => [v.size, v.color].filter(Boolean).join(' / ') || '—' },
    { title: 'Price', dataIndex: 'price', key: 'price', render: (p: number) => `$${p}` },
    {
      title: 'Stock',
      key: 'stock',
      render: (_: unknown, v: Variant) => (
        <Space>
          <span>{v.stockQuantity}</span>
          <Button size="small" onClick={() => handleAdjust(v.id, -1)}>
            −
          </Button>
          <Button size="small" onClick={() => handleAdjust(v.id, 1)}>
            +
          </Button>
        </Space>
      ),
    },
    {
      title: '',
      key: 'actions',
      render: (_: unknown, v: Variant) => (
        <Button type="link" danger onClick={() => deleteVariant({ productId, variantId: v.id })}>
          Delete
        </Button>
      ),
    },
  ];

  return <Table rowKey="id" columns={columns} dataSource={variants} pagination={false} size="small" />;
});
```

- [ ] **Step 5: Wire `VariantsTable` into `ProductDetailPage`**

```typescript
// frontend/src/features/products/ProductDetailPage.tsx  (replace entire file)
import { useParams } from 'react-router-dom';
import { Spin, Typography } from 'antd';
import { useGetProductQuery } from './api';
import { ProductForm } from './ProductForm';
import { VariantsTable } from './VariantsTable';

export function ProductDetailPage() {
  const { id } = useParams<{ id: string }>();
  const isCreate = !id;
  const productId = id ? Number(id) : undefined;

  const { data: product, isLoading } = useGetProductQuery(productId!, { skip: isCreate });

  if (!isCreate && isLoading) return <Spin size="large" />;

  return (
    <div>
      <Typography.Title level={3}>{isCreate ? 'New Product' : product?.name}</Typography.Title>
      <ProductForm product={product ?? null} />
      {!isCreate && productId && (
        <div style={{ marginTop: 24 }}>
          <Typography.Title level={5}>Variants</Typography.Title>
          <VariantsTable productId={productId} variants={product?.variants ?? []} />
        </div>
      )}
    </div>
  );
}
```

(`ImageUploader` is added alongside this in Task 8 — not included here to
keep this task's diff focused on variants/stock.)

- [ ] **Step 6: Write the failing tests — this is the frontend's proof that the optimistic-update/rollback flow actually works**

```typescript
// frontend/src/features/products/VariantsTable.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import { describe, expect, it } from 'vitest';
import { api } from '../../shared/lib/apiBase';
import { mockProducts, resetMockProducts } from '../../mocks/handlers';
import { VariantsTable } from './VariantsTable';
import { Variant } from './types';

// The component's `variants` prop is only for display - the actual PATCH request
// goes through the real MSW handler, which looks the variant up inside
// mockProducts[0].variants by id. Both must describe the same variant, or the
// mock server 404s instead of exercising the intended success/conflict path.
const variant: Variant = {
  id: 1, sku: 'TEE-M', size: 'M', color: 'Blue', price: 20, compareAtPrice: null, stockQuantity: 5, barcode: null, isActive: true,
};

function renderTable(variants: Variant[]) {
  mockProducts[0].variants = variants;
  const store = configureStore({
    reducer: { [api.reducerPath]: api.reducer },
    middleware: (getDefault) => getDefault().concat(api.middleware),
  });
  return render(
    <Provider store={store}>
      <VariantsTable productId={mockProducts[0].id} variants={variants} />
    </Provider>,
  );
}

describe('VariantsTable', () => {
  it('shows the current stock quantity for each variant', () => {
    resetMockProducts();
    renderTable([variant]);

    expect(screen.getByText('5')).toBeInTheDocument();
  });

  it('shows an insufficient-stock message with the available quantity on a 409', async () => {
    resetMockProducts();
    const zeroStockVariant = { ...variant, stockQuantity: 0 };
    renderTable([zeroStockVariant]);

    await userEvent.click(screen.getByText('−')); // one decrement against 0 in stock - deterministically rejected, no race on click timing

    expect(await screen.findByText(/only 0 left/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 7: Run to verify failure, then confirm they pass**

```bash
npx vitest run VariantsTable.test.tsx
```

Expected: PASS, 2 tests. Note what this specific test does and doesn't
prove: `VariantsTable` here receives `variants` as a plain prop (not read
from the RTK Query cache directly), so this confirms the mutation fires
correctly and the `409`/`availableQuantity` error path surfaces the right
message — not the optimistic-cache-patch-then-rollback visual behavior
itself, which only manifests where `useGetProductQuery` actually owns the
data (`ProductDetailPage`, wiring both components together via `unwrap()`
already exercised by `ProductDetailPage.test.tsx`, Task 6).

- [ ] **Step 8: Commit**

```bash
git add frontend/src/features/products frontend/src/mocks/handlers.ts
git commit -m "Add VariantsTable with optimistic stock adjustment and Idempotency-Key"
```

---

## Task 8: Image Uploader (spec §3, §6, §8 — the file-uploader requirement)

**Files:**
- Modify: `frontend/src/features/products/api.ts`
- Create: `frontend/src/features/products/ImageUploader.tsx`
- Modify: `frontend/src/features/products/ProductDetailPage.tsx`
- Modify: `frontend/src/mocks/handlers.ts`
- Test: `frontend/src/features/products/ImageUploader.test.tsx`

- [ ] **Step 1: Add upload/delete endpoints to `api.ts`**

```typescript
// frontend/src/features/products/api.ts  (add inside the existing `injectEndpoints` call)
    uploadImage: builder.mutation<{ imageUrl: string }, { productId: number; formData: FormData }>({
      query: ({ productId, formData }) => ({
        url: `/products/${productId}/image`,
        method: 'POST',
        data: formData,
        headers: { 'Content-Type': 'multipart/form-data' },
      }),
      invalidatesTags: (_result, _error, { productId }) => [{ type: 'Product', id: productId }],
    }),
    deleteImage: builder.mutation<void, number>({
      query: (productId) => ({ url: `/products/${productId}/image`, method: 'DELETE' }),
      invalidatesTags: (_result, _error, productId) => [{ type: 'Product', id: productId }],
    }),
```

Add `useUploadImageMutation`, `useDeleteImageMutation` to the exports.

- [ ] **Step 2: Add MSW handlers**

```typescript
// frontend/src/mocks/handlers.ts  (append)
http.post(`${baseUrl}/products/:productId/image`, async ({ params }) => {
  const product = mockProducts.find((p) => p.id === Number(params.productId));
  if (!product) return new HttpResponse(null, { status: 404 });
  const imageUrl = `/uploads/products/${product.id}/mock.jpg`;
  product.imageUrl = imageUrl;
  return HttpResponse.json({ imageUrl });
}),

http.delete(`${baseUrl}/products/:productId/image`, ({ params }) => {
  const product = mockProducts.find((p) => p.id === Number(params.productId));
  if (!product?.imageUrl) return new HttpResponse(null, { status: 404 });
  product.imageUrl = null;
  return new HttpResponse(null, { status: 204 });
}),
```

- [ ] **Step 3: Write `ImageUploader`, with client-side validation matching backend rules exactly (spec §8)**

```typescript
// frontend/src/features/products/ImageUploader.tsx
import { Upload, Button, message } from 'antd';
import type { UploadProps } from 'antd';
import { useUploadImageMutation, useDeleteImageMutation } from './api';
import { AppError } from '../../shared/lib/errors';

interface Props {
  productId: number;
  imageUrl: string | null;
}

const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp'];
const MAX_SIZE_BYTES = 5 * 1024 * 1024;

export function ImageUploader({ productId, imageUrl }: Props) {
  const [uploadImage, { isLoading: uploading }] = useUploadImageMutation();
  const [deleteImage] = useDeleteImageMutation();

  const beforeUpload: UploadProps['beforeUpload'] = (file) => {
    if (!ALLOWED_TYPES.includes(file.type)) {
      message.error('Only JPEG, PNG, or WEBP images are allowed.');
      return Upload.LIST_IGNORE;
    }
    if (file.size > MAX_SIZE_BYTES) {
      message.error('Image must be 5 MB or smaller.');
      return Upload.LIST_IGNORE;
    }
    return true;
  };

  const customRequest: UploadProps['customRequest'] = async (options) => {
    try {
      const formData = new FormData();
      formData.append('file', options.file as Blob);
      await uploadImage({ productId, formData }).unwrap();
      options.onSuccess?.({});
    } catch (err) {
      message.error((err as AppError).message);
      options.onError?.(err as Error);
    }
  };

  async function handleRemove() {
    try {
      await deleteImage(productId).unwrap();
    } catch (err) {
      message.error((err as AppError).message);
    }
  }

  if (imageUrl) {
    return (
      <div>
        <img
          src={imageUrl}
          alt="Product"
          style={{ width: 120, height: 120, objectFit: 'cover' }}
          onError={(e) => {
            (e.target as HTMLImageElement).style.visibility = 'hidden'; // broken-image fallback (spec section 8)
          }}
        />
        <Button size="small" onClick={handleRemove} style={{ display: 'block', marginTop: 4 }}>
          Remove
        </Button>
      </div>
    );
  }

  return (
    <Upload beforeUpload={beforeUpload} customRequest={customRequest} showUploadList={false}>
      <Button loading={uploading}>Upload Image</Button>
    </Upload>
  );
}
```

- [ ] **Step 4: Wire `ImageUploader` into `ProductDetailPage`, matching the confirmed layout (image + fields at the top — spec §6)**

```typescript
// frontend/src/features/products/ProductDetailPage.tsx  (replace entire file)
import { useParams } from 'react-router-dom';
import { Spin, Typography } from 'antd';
import { useGetProductQuery } from './api';
import { ProductForm } from './ProductForm';
import { VariantsTable } from './VariantsTable';
import { ImageUploader } from './ImageUploader';

export function ProductDetailPage() {
  const { id } = useParams<{ id: string }>();
  const isCreate = !id;
  const productId = id ? Number(id) : undefined;

  const { data: product, isLoading } = useGetProductQuery(productId!, { skip: isCreate });

  if (!isCreate && isLoading) return <Spin size="large" />;

  return (
    <div>
      <Typography.Title level={3}>{isCreate ? 'New Product' : product?.name}</Typography.Title>
      <div style={{ display: 'flex', gap: 24, marginBottom: 24 }}>
        {!isCreate && productId && <ImageUploader productId={productId} imageUrl={product?.imageUrl ?? null} />}
        <div style={{ flex: 1 }}>
          <ProductForm product={product ?? null} />
        </div>
      </div>
      {!isCreate && productId && (
        <div>
          <Typography.Title level={5}>Variants</Typography.Title>
          <VariantsTable productId={productId} variants={product?.variants ?? []} />
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 5: Write the failing tests — client-side validation is the important behavior to prove here**

```typescript
// frontend/src/features/products/ImageUploader.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import { describe, expect, it, vi } from 'vitest';
import { api } from '../../shared/lib/apiBase';
import { ImageUploader } from './ImageUploader';

function renderUploader(imageUrl: string | null = null) {
  const store = configureStore({
    reducer: { [api.reducerPath]: api.reducer },
    middleware: (getDefault) => getDefault().concat(api.middleware),
  });
  return render(
    <Provider store={store}>
      <ImageUploader productId={1} imageUrl={imageUrl} />
    </Provider>,
  );
}

describe('ImageUploader', () => {
  it('rejects a non-image file client-side, before any network call', async () => {
    renderUploader();
    const input = screen.getByRole('button', { name: /upload image/i }).parentElement!.querySelector('input')!;
    const badFile = new File(['x'], 'doc.pdf', { type: 'application/pdf' });

    await userEvent.upload(input, badFile);

    expect(await screen.findByText(/only jpeg, png, or webp/i)).toBeInTheDocument();
  });

  it('rejects an oversized file client-side', async () => {
    renderUploader();
    const input = screen.getByRole('button', { name: /upload image/i }).parentElement!.querySelector('input')!;
    const bigFile = new File([new Uint8Array(6 * 1024 * 1024)], 'big.jpg', { type: 'image/jpeg' });

    await userEvent.upload(input, bigFile);

    expect(await screen.findByText(/5 mb or smaller/i)).toBeInTheDocument();
  });

  it('shows the existing image with a Remove button when one is already set', () => {
    renderUploader('/uploads/products/1/photo.jpg');

    expect(screen.getByAltText('Product')).toHaveAttribute('src', '/uploads/products/1/photo.jpg');
    expect(screen.getByText('Remove')).toBeInTheDocument();
  });
});
```

- [ ] **Step 6: Run to verify failure, then confirm they pass**

```bash
npx vitest run ImageUploader.test.tsx
```

Expected: PASS, 3 tests.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/features/products frontend/src/mocks/handlers.ts
git commit -m "Add ImageUploader with client-side validation matching backend rules"
```

---

## Task 9: Dedicated Concurrent-Edit-Conflict UX (spec §8)

Replaces Task 6's placeholder `message.error(...)` on a `409` with the
dedicated UX the spec actually calls for: a named explanation and a
**Reload latest** action, not a generic toast.

**Files:**
- Modify: `frontend/src/features/products/ProductForm.tsx`
- Test: `frontend/src/features/products/ProductForm.conflict.test.tsx`

- [ ] **Step 1: Write the failing test**

```typescript
// frontend/src/features/products/ProductForm.conflict.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import { describe, expect, it } from 'vitest';
import { api } from '../../shared/lib/apiBase';
import { ProductForm } from './ProductForm';
import { Product } from './types';

const staleProduct: Product = {
  id: 1, name: 'Classic Cotton Tee', slug: 'classic-cotton-tee', description: null,
  categoryId: 2, brand: 'Acme', status: 'Active', attributes: '{}', imageUrl: null, variants: [],
};

function renderForm() {
  const store = configureStore({
    reducer: { [api.reducerPath]: api.reducer },
    middleware: (getDefault) => getDefault().concat(api.middleware),
  });
  return render(
    <Provider store={store}>
      <ProductForm product={staleProduct} />
    </Provider>,
  );
}

describe('ProductForm concurrent-edit conflict', () => {
  it('shows the dedicated conflict modal (not a generic toast) on a 409, with a Reload latest action', async () => {
    // The shared MSW handler for PUT /products/:id (Task 6) rejects with 409 whenever
    // If-Match isn't exactly "1" - the etagStore never received an ETag for this product
    // in this render (no prior GET happened), so the interceptor sends no If-Match at
    // all, which the mock treats as a mismatch - reliably reproducing the conflict path.
    renderForm();

    await userEvent.clear(screen.getByLabelText('Name'));
    await userEvent.type(screen.getByLabelText('Name'), 'Updated Name');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByText(/changed by someone else/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /reload latest/i })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run to verify failure**

```bash
npx vitest run ProductForm.conflict.test.tsx
```

Expected: FAIL — the current `ProductForm` only shows a `message.error` toast, no modal, no "Reload latest" button.

- [ ] **Step 3: Replace the placeholder with the dedicated conflict modal**

```typescript
// frontend/src/features/products/ProductForm.tsx  (replace entire file)
import { useState } from 'react';
import { Form, Input, Select, Button, Modal, message } from 'antd';
import { useDispatch } from 'react-redux';
import { useNavigate } from 'react-router-dom';
import { useCreateProductMutation, useUpdateProductMutation, productsApi } from './api';
import { useListCategoriesQuery } from '../categories/api';
import { Product } from './types';
import { AppError } from '../../shared/lib/errors';

interface Props {
  product: Product | null;
}

export function ProductForm({ product }: Props) {
  const [form] = Form.useForm();
  const navigate = useNavigate();
  const dispatch = useDispatch();
  const { data: categories } = useListCategoriesQuery();
  const [createProduct, { isLoading: creating }] = useCreateProductMutation();
  const [updateProduct, { isLoading: updating }] = useUpdateProductMutation();
  const [conflictOpen, setConflictOpen] = useState(false);

  const handleFinish = async (values: { name: string; slug: string; categoryId: number; brand?: string; description?: string }) => {
    try {
      if (product) {
        await updateProduct({
          id: product.id,
          body: {
            name: values.name,
            description: values.description ?? null,
            categoryId: values.categoryId,
            brand: values.brand ?? null,
            attributes: product.attributes,
          },
        }).unwrap();
        message.success('Product updated.');
      } else {
        const created = await createProduct({
          name: values.name,
          slug: values.slug,
          categoryId: values.categoryId,
          brand: values.brand ?? null,
          description: values.description ?? null,
          attributes: '{}',
          variants: [],
        }).unwrap();
        message.success('Product created.');
        navigate(`/products/${created.id}`);
      }
    } catch (err) {
      const appError = err as AppError;
      if (appError.status === 409) {
        setConflictOpen(true);
      } else if (appError.fieldErrors) {
        form.setFields(
          appError.fieldErrors.map((fe) => ({
            name: fe.propertyName.charAt(0).toLowerCase() + fe.propertyName.slice(1),
            errors: [fe.errorMessage],
          })),
        );
      } else {
        message.error(appError.message);
      }
    }
  };

  function handleReloadLatest() {
    if (product) dispatch(productsApi.util.invalidateTags([{ type: 'Product', id: product.id }]));
    setConflictOpen(false);
  }

  return (
    <>
      <Form
        form={form}
        layout="vertical"
        initialValues={
          product
            ? { name: product.name, slug: product.slug, categoryId: product.categoryId, brand: product.brand ?? undefined, description: product.description ?? undefined }
            : {}
        }
        onFinish={handleFinish}
      >
        <Form.Item name="name" label="Name" rules={[{ required: true, max: 200 }]}>
          <Input />
        </Form.Item>
        {!product && (
          <Form.Item name="slug" label="Slug" rules={[{ required: true, pattern: /^[a-z0-9-]+$/, message: 'Lowercase letters, numbers, and hyphens only.' }]}>
            <Input />
          </Form.Item>
        )}
        <Form.Item name="categoryId" label="Category" rules={[{ required: true }]}>
          <Select options={categories?.map((c) => ({ label: c.name, value: c.id }))} />
        </Form.Item>
        <Form.Item name="brand" label="Brand">
          <Input />
        </Form.Item>
        <Form.Item name="description" label="Description">
          <Input.TextArea rows={3} />
        </Form.Item>
        <Button type="primary" htmlType="submit" loading={creating || updating}>
          Save
        </Button>
      </Form>

      <Modal
        open={conflictOpen}
        onCancel={() => setConflictOpen(false)}
        title="This product was changed by someone else"
        footer={[
          <Button key="reload" type="primary" onClick={handleReloadLatest}>
            Reload latest
          </Button>,
        ]}
      >
        <p>Someone else updated this product while you were editing it. Reload to see the latest version, then re-apply your changes.</p>
      </Modal>
    </>
  );
}
```

- [ ] **Step 4: Run to verify it passes**

```bash
npx vitest run ProductForm.conflict.test.tsx
```

Expected: PASS, 1 test. Then run the full products test suite once more
to confirm this replacement didn't regress Task 6's other `ProductForm`
tests:

```bash
npx vitest run ProductDetailPage.test.tsx ProductForm.conflict.test.tsx
```

Expected: PASS, all tests.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/features/products/ProductForm.tsx frontend/src/features/products/ProductForm.conflict.test.tsx
git commit -m "Replace generic conflict toast with dedicated Reload-latest modal on 409"
```

---

## Task 10: `web` Docker Service, Dockerfile, README, and Final Verification (spec §9)

Extends the **existing** `docker-compose.yml` from the backend plan's
Task 17 — this task adds a fourth service to that same file, at the repo
root, rather than creating a new one.

**Files:**
- Create: `frontend/Dockerfile`
- Create: `frontend/nginx.conf`
- Create: `frontend/.dockerignore`
- Modify: `docker-compose.yml` (repo root)
- Modify: `README.md` (repo root)

- [ ] **Step 1: Write the multi-stage `Dockerfile` (Vite build stage, then nginx serves the static output)**

```dockerfile
# frontend/Dockerfile
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 5173
```

- [ ] **Step 2: Write `nginx.conf` — SPA fallback so client-side routes (e.g. `/products/42`) don't 404 on a hard refresh**

```nginx
# frontend/nginx.conf
server {
    listen 5173;
    root /usr/share/nginx/html;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

- [ ] **Step 3: Write `.dockerignore`**

```
# frontend/.dockerignore
node_modules/
dist/
```

- [ ] **Step 4: Add the `web` service to the repo-root `docker-compose.yml`**

```yaml
# docker-compose.yml  (add this service to the existing file from the backend plan's Task 17)
  web:
    build:
      context: ./frontend
    ports:
      - "5173:5173"
    depends_on:
      - api
```

`VITE_API_BASE_URL` doesn't need to be passed as a build arg here — it's
already baked in from `frontend/.env` at `npm run build` time (Task 0),
and that value (`http://localhost:8080/api/v1`) is correct in every mode
this app runs in, per spec §9.

- [ ] **Step 5: Bring up the full stack and verify manually end-to-end**

```bash
docker compose up --build -d
```

Expected: all four containers running. Then open
`http://localhost:5173` in a browser — the product list should load
(populated by the backend's seed data), and the sidebar should show
Products/Categories navigation.

- [ ] **Step 6: Extend the repo-root `README.md` with the front-end sections**

```markdown
<!-- README.md  (append to the file from the backend plan's Task 18) -->

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
```

- [ ] **Step 7: Run the entire front-end test suite one final time**

```bash
cd frontend
npm run build
npm test
```

Expected: build succeeds, every test file passes — including the
optimistic-stock-update test (Task 7) and the concurrent-edit-conflict
test (Task 9), the two tests that most directly prove this front-end
correctly integrates with the backend's strong-consistency guarantees
rather than just rendering data.

- [ ] **Step 8: Tear down and commit**

```bash
docker compose down -v
git add frontend/Dockerfile frontend/nginx.conf frontend/.dockerignore docker-compose.yml README.md
git commit -m "Add web docker service, nginx config, and complete the front-end implementation"
```

---

## Done

At this point: a full React admin front-end — sidebar shell, Categories
(tree table) and Products (data table, search, cursor pagination) list
views, a single-stacked-page product editor with image upload and inline
variant/stock management, optimistic stock updates with automatic
rollback, a dedicated concurrent-edit-conflict UX, and MSW-backed tests
throughout. `docker compose up --build` from the repo root now brings up
the complete four-service stack — postgres, redis, api, and web — for a
reviewer to open in one command.

