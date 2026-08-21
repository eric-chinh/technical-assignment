import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import { MemoryRouter } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { api } from '../../shared/lib/apiBase';
import { server } from '../../mocks/server';
import { resetMockProducts } from '../../mocks/handlers';
import { ProductListPage } from './ProductListPage';

function renderPage() {
  const store = configureStore({
    reducer: { [api.reducerPath]: api.reducer },
    middleware: (getDefault) => getDefault().concat(api.middleware),
  });
  return render(
    <Provider store={store}>
      <MemoryRouter>
        <ProductListPage />
      </MemoryRouter>
    </Provider>,
  );
}

describe('ProductListPage', () => {
  it('renders the seeded mock product', async () => {
    resetMockProducts();
    renderPage();

    expect(await screen.findByText('Classic Cotton Tee')).toBeInTheDocument();
  });

  it('shows the default placeholder image for a product with no imageUrl', async () => {
    resetMockProducts();
    renderPage();
    const row = (await screen.findByText('Classic Cotton Tee')).closest('tr')!;

    const image = row.querySelector('img')!;
    expect(image).toHaveAttribute('src', '/product-placeholder.svg');
  });

  it('shows the total count and page number', async () => {
    resetMockProducts();
    renderPage();

    expect(await screen.findByText('Showing 1–1 of 1 products · Page 1 of 1')).toBeInTheDocument();
  });

  it('shows the empty state when a search matches nothing', async () => {
    resetMockProducts();
    renderPage();
    await screen.findByText('Classic Cotton Tee');

    await userEvent.type(screen.getByPlaceholderText('Search products...'), 'nonexistent-product-xyz');

    expect(await screen.findByText(/no products match these filters/i)).toBeInTheDocument();
    expect(await screen.findByText('No products')).toBeInTheDocument();
  });

  it('shows a retry-able error state, not a blank table, when the API is unreachable', async () => {
    server.use(http.get('http://localhost:8080/api/v1/products', () => HttpResponse.error()));
    renderPage();

    expect(await screen.findByText(/couldn't load products/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument();
  });
});
