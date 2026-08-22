import { api } from '../../shared/lib/apiBase';
import type {
  Product,
  ProductListItem,
  PagedResult,
  CreateProductRequest,
  UpdateProductRequest,
  ProductItem,
  CreateProductItemRequest,
  UpdateProductItemRequest,
  AdjustStockResult,
  Variation,
  Promotion,
} from './types';

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
    createProductItem: builder.mutation<ProductItem, { productId: number; body: CreateProductItemRequest }>({
      query: ({ productId, body }) => ({ url: `/products/${productId}/items`, method: 'POST', data: body }),
      invalidatesTags: (_result, _error, { productId }) => [{ type: 'Product', id: productId }],
    }),
    updateProductItem: builder.mutation<ProductItem, { itemId: number; version: number; body: UpdateProductItemRequest }>({
      query: ({ itemId, version, body }) => ({
        url: `/product-items/${itemId}`,
        method: 'PATCH',
        data: body,
        headers: { 'If-Match': `"${version}"` },
      }),
    }),
    deleteProductItem: builder.mutation<void, { productId: number; itemId: number }>({
      query: ({ itemId }) => ({ url: `/product-items/${itemId}`, method: 'DELETE' }),
      invalidatesTags: (_result, _error, { productId }) => [{ type: 'Product', id: productId }],
    }),
    adjustStock: builder.mutation<AdjustStockResult, { productId: number; itemId: number; delta: number }>({
      query: ({ itemId, delta }) => ({
        url: `/product-items/${itemId}/inventory/adjust`,
        method: 'POST',
        data: { delta },
        headers: { 'Idempotency-Key': crypto.randomUUID() },
      }),
      async onQueryStarted({ productId, itemId, delta }, { dispatch, queryFulfilled }) {
        // Instant UI feedback (spec section 5) - the cached stock number updates before
        // the network round-trip completes, then rolls back automatically on failure.
        const patch = dispatch(
          productsApi.util.updateQueryData('getProduct', productId, (draft) => {
            const item = draft.items.find((i) => i.id === itemId);
            if (item) item.qtyInStock += delta;
          }),
        );
        try {
          await queryFulfilled;
        } catch {
          patch.undo();
        }
      },
    }),
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
    listVariations: builder.query<Variation[], number>({
      query: (categoryId) => ({ url: `/categories/${categoryId}/variations`, method: 'GET' }),
    }),
    listPromotions: builder.query<Promotion[], void>({
      query: () => ({ url: '/promotions', method: 'GET' }),
    }),
  }),
});

export const {
  useListProductsQuery,
  useGetProductQuery,
  useCreateProductMutation,
  useUpdateProductMutation,
  useDeleteProductMutation,
  useCreateProductItemMutation,
  useUpdateProductItemMutation,
  useDeleteProductItemMutation,
  useAdjustStockMutation,
  useUploadImageMutation,
  useDeleteImageMutation,
  useListVariationsQuery,
  useListPromotionsQuery,
} = productsApi;
