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
