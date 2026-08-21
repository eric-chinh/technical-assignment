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
