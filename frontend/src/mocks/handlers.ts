import { http, HttpResponse } from 'msw';
import type { Category } from '../features/categories/types';
import type { Product, ProductListItem } from '../features/products/types';

const baseUrl = 'http://localhost:8080/api/v1';

function initialMockProducts(): Product[] {
  return [
    {
      id: 1, name: 'Classic Cotton Tee', slug: 'classic-cotton-tee', description: null,
      categoryId: 2, brand: 'Acme', status: 'Active', attributes: '{}', imageUrl: null,
      items: [{ id: 1, sku: 'TEE-M', price: 20, qtyInStock: 50, productImage: null, isActive: true, version: 1, variationOptionIds: [] }],
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

  http.get(`${baseUrl}/products`, ({ request }) => {
    const url = new URL(request.url);
    const q = url.searchParams.get('q');
    const filtered = q ? mockProducts.filter((p) => p.name.toLowerCase().includes(q.toLowerCase())) : mockProducts;
    const items: ProductListItem[] = filtered.map((p) => ({
      id: p.id, name: p.name, slug: p.slug, categoryId: p.categoryId, brand: p.brand, status: p.status,
      minPrice: p.items[0]?.price ?? null, maxPrice: p.items[0]?.price ?? null,
      totalStock: p.items.reduce((sum, v) => sum + v.qtyInStock, 0), imageUrl: p.imageUrl,
    }));
    return HttpResponse.json({ items, nextCursor: null, hasMore: false, totalCount: filtered.length });
  }),

  http.get(`${baseUrl}/products/:id`, ({ params }) => {
    const product = mockProducts.find((p) => p.id === Number(params.id));
    if (!product) return new HttpResponse(null, { status: 404 });
    return HttpResponse.json(product, { headers: { ETag: '"1"' } });
  }),

  http.post(`${baseUrl}/products`, async ({ request }) => {
    const body = (await request.json()) as { name: string; slug: string; categoryId: number; brand: string | null; description: string | null };
    const created: Product = {
      id: mockProducts.length + 1, ...body, attributes: '{}', imageUrl: null, status: 'Draft', items: [],
    };
    mockProducts.push(created);
    return HttpResponse.json(created, { status: 201, headers: { Location: `/products/${created.id}` } });
  }),

  http.put(`${baseUrl}/products/:id`, async ({ params, request }) => {
    const product = mockProducts.find((p) => p.id === Number(params.id));
    if (!product) return new HttpResponse(null, { status: 404 });
    const ifMatch = request.headers.get('If-Match');
    // Mirrors the real backend's ParseIfMatch: a missing/unparseable header defaults to a
    // version that never matches a real xmin, so it's a guaranteed conflict, not a pass-through.
    if (ifMatch !== '"1"') {
      return HttpResponse.json({ title: 'Concurrency conflict.', status: 409 }, { status: 409 });
    }
    const body = (await request.json()) as { name: string; description: string | null; categoryId: number; brand: string | null };
    Object.assign(product, body);
    return HttpResponse.json(product, { headers: { ETag: '"1"' } });
  }),

  http.post(`${baseUrl}/products/:productId/items`, async ({ params, request }) => {
    const product = mockProducts.find((p) => p.id === Number(params.productId));
    if (!product) return new HttpResponse(null, { status: 404 });
    const body = (await request.json()) as { sku: string; price: number; qtyInStock: number; productImage: string | null };
    if (product.items.some((v) => v.sku === body.sku)) {
      return HttpResponse.json({ title: 'SKU already exists.', status: 409 }, { status: 409 });
    }
    const created = { id: product.items.length + 100, isActive: true, version: 1, variationOptionIds: [], ...body };
    product.items.push(created);
    return HttpResponse.json(created, { status: 201 });
  }),

  http.delete(`${baseUrl}/product-items/:itemId`, ({ params }) => {
    for (const product of mockProducts) {
      const item = product.items.find((v) => v.id === Number(params.itemId));
      if (item) {
        item.isActive = false; // soft delete, mirrors the real backend's ProductItem.Deactivate()
        return new HttpResponse(null, { status: 204 });
      }
    }
    return new HttpResponse(null, { status: 404 });
  }),

  http.post(`${baseUrl}/product-items/:itemId/inventory/adjust`, async ({ params, request }) => {
    for (const product of mockProducts) {
      const item = product.items.find((v) => v.id === Number(params.itemId));
      if (item) {
        const { delta } = (await request.json()) as { delta: number };
        if (delta < 0 && item.qtyInStock + delta < 0) {
          return HttpResponse.json(
            { succeeded: false, newQuantity: null, availableQuantity: item.qtyInStock },
            { status: 409 },
          );
        }
        item.qtyInStock += delta;
        return HttpResponse.json({ succeeded: true, newQuantity: item.qtyInStock, availableQuantity: null });
      }
    }
    return new HttpResponse(null, { status: 404 });
  }),

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
];
