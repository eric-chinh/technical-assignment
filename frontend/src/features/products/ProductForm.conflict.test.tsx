import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { api } from '../../shared/lib/apiBase';
import { ProductForm } from './ProductForm';
import type { Product } from './types';

const staleProduct: Product = {
  id: 1, name: 'Classic Cotton Tee', slug: 'classic-cotton-tee', description: null,
  categoryId: 2, brand: 'Acme', status: 'Active', attributes: '{}', imageUrl: null, items: [],
};

function renderForm() {
  const store = configureStore({
    reducer: { [api.reducerPath]: api.reducer },
    middleware: (getDefault) => getDefault().concat(api.middleware),
  });
  return render(
    <Provider store={store}>
      <MemoryRouter>
        <ProductForm product={staleProduct} />
      </MemoryRouter>
    </Provider>,
  );
}

describe('ProductForm concurrent-edit conflict', () => {
  it('shows the dedicated conflict modal (not a generic toast) on a 409, with a Reload latest action', async () => {
    // The shared MSW handler for PUT /products/:id (Task 6) rejects with 409 whenever
    // If-Match isn't exactly "1" - the etagStore never received an ETag for this product
    // in this render (no prior GET happened), so the interceptor sends no If-Match at
    // all, which the mock treats as a mismatch - reliably reproducing the conflict path.
    renderForm();

    await userEvent.clear(screen.getByLabelText('Name'));
    await userEvent.type(screen.getByLabelText('Name'), 'Updated Name');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByText(/changed by someone else/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /reload latest/i })).toBeInTheDocument();
  });
});
