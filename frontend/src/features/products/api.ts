import { api } from '../../shared/lib/apiBase';
import type { Product, ProductListItem, PagedResult, CreateProductRequest, UpdateProductRequest } from './types';

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
