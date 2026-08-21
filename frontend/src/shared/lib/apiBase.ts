import { createApi } from '@reduxjs/toolkit/query/react';
import type { BaseQueryFn } from '@reduxjs/toolkit/query';
import type { AxiosRequestConfig } from 'axios';
import { axiosClient } from './axiosClient';
import type { AppError } from './errors';

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
