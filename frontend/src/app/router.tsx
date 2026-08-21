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
