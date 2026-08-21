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
