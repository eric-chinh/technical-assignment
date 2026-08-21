import { api } from '../../shared/lib/apiBase';
import type {
  Product,
  ProductListItem,
  PagedResult,
  CreateProductRequest,
  UpdateProductRequest,
  Variant,
  CreateVariantRequest,
  AdjustStockResult,
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
  }),
});

export const {
  useListProductsQuery,
  useGetProductQuery,
  useCreateProductMutation,
  useUpdateProductMutation,
  useDeleteProductMutation,
  useCreateVariantMutation,
  useDeleteVariantMutation,
  useAdjustStockMutation,
  useUploadImageMutation,
  useDeleteImageMutation,
} = productsApi;
