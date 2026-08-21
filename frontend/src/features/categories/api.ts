import { api } from '../../shared/lib/apiBase';
import type { Category, CreateCategoryRequest, UpdateCategoryRequest } from './types';

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
