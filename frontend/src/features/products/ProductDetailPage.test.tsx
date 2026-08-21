import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { api } from '../../shared/lib/apiBase';
import { resetMockProducts, resetMockCategories } from '../../mocks/handlers';
import { ProductDetailPage } from './ProductDetailPage';

function renderAt(path: string) {
  const store = configureStore({
    reducer: { [api.reducerPath]: api.reducer },
    middleware: (getDefault) => getDefault().concat(api.middleware),
  });
  return render(
    <Provider store={store}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/products/new" element={<ProductDetailPage />} />
          <Route path="/products/:id" element={<ProductDetailPage />} />
        </Routes>
      </MemoryRouter>
    </Provider>,
  );
}

describe('ProductDetailPage', () => {
  it('create mode shows an empty form with a Slug field', async () => {
    resetMockProducts();
    renderAt('/products/new');

    expect(await screen.findByText('New Product')).toBeInTheDocument();
    expect(screen.getByLabelText('Slug')).toBeInTheDocument();
  });

  it('edit mode loads and displays the existing product name, without a Slug field', async () => {
    resetMockProducts();
    renderAt('/products/1');

    expect(await screen.findByText('Classic Cotton Tee')).toBeInTheDocument();
    expect(screen.queryByLabelText('Slug')).not.toBeInTheDocument();
  });

  it('submitting valid create-mode data creates the product and navigates to its detail page', async () => {
    resetMockProducts();
    resetMockCategories();
    renderAt('/products/new');
    await screen.findByText('New Product');

    await userEvent.type(screen.getByLabelText('Name'), 'New Jacket');
    await userEvent.type(screen.getByLabelText('Slug'), 'new-jacket');
    await userEvent.click(screen.getByLabelText('Category'));
    await userEvent.click(await screen.findByText('Women'));
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    // Successful create navigates to /products/2 (the seeded product is id 1), which
    // re-renders this same component in edit mode showing the just-created product.
    expect(await screen.findByText('New Jacket')).toBeInTheDocument();
  });
});
