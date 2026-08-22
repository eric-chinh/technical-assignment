import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import { describe, expect, it } from 'vitest';
import { api } from '../../shared/lib/apiBase';
import { mockProducts, resetMockProducts } from '../../mocks/handlers';
import { VariantsTable } from './VariantsTable';
import type { ProductItem } from './types';

// The component's `items` prop is only for display - the actual POST request
// goes through the real MSW handler, which looks the item up inside
// mockProducts[0].items by id. Both must describe the same item, or the
// mock server 404s instead of exercising the intended success/conflict path.
const item: ProductItem = {
  id: 1, sku: 'TEE-M', price: 20, qtyInStock: 5, productImage: null, isActive: true, version: 1, variationOptionIds: [],
};

function renderTable(items: ProductItem[]) {
  mockProducts[0].items = items;
  const store = configureStore({
    reducer: { [api.reducerPath]: api.reducer },
    middleware: (getDefault) => getDefault().concat(api.middleware),
  });
  return render(
    <Provider store={store}>
      <VariantsTable productId={mockProducts[0].id} items={items} />
    </Provider>,
  );
}

describe('VariantsTable', () => {
  it('shows the current stock quantity for each item', () => {
    resetMockProducts();
    renderTable([item]);

    expect(screen.getByText('5')).toBeInTheDocument();
  });

  it('shows an insufficient-stock message with the available quantity on a 409', async () => {
    resetMockProducts();
    const zeroStockItem = { ...item, qtyInStock: 0 };
    renderTable([zeroStockItem]);

    await userEvent.click(screen.getByText('−')); // one decrement against 0 in stock - deterministically rejected, no race on click timing

    expect(await screen.findByText(/only 0 left/i)).toBeInTheDocument();
  });

  it('deleting a variant requires confirmation, then fires the request and confirms success', async () => {
    resetMockProducts();
    renderTable([item]);

    await userEvent.click(screen.getByText('Delete'));
    // Popconfirm doesn't fire the mutation until the user confirms - clicking Delete alone must not be enough.
    expect(screen.queryByText('Variant deleted.')).not.toBeInTheDocument();

    await userEvent.click(await screen.findByRole('button', { name: /ok|yes/i }));

    expect(await screen.findByText('Variant deleted.')).toBeInTheDocument();
  });

  it('does not display an item the backend has soft-deleted (isActive: false)', () => {
    resetMockProducts();
    const deletedItem = { ...item, isActive: false };
    renderTable([deletedItem]);

    expect(screen.queryByText('TEE-M')).not.toBeInTheDocument();
  });
});
