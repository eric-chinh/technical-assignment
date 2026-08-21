import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import { describe, expect, it } from 'vitest';
import { api } from '../../shared/lib/apiBase';
import { mockProducts, resetMockProducts } from '../../mocks/handlers';
import { VariantsTable } from './VariantsTable';
import type { Variant } from './types';

// The component's `variants` prop is only for display - the actual PATCH request
// goes through the real MSW handler, which looks the variant up inside
// mockProducts[0].variants by id. Both must describe the same variant, or the
// mock server 404s instead of exercising the intended success/conflict path.
const variant: Variant = {
  id: 1, sku: 'TEE-M', size: 'M', color: 'Blue', price: 20, compareAtPrice: null, stockQuantity: 5, barcode: null, isActive: true,
};

function renderTable(variants: Variant[]) {
  mockProducts[0].variants = variants;
  const store = configureStore({
    reducer: { [api.reducerPath]: api.reducer },
    middleware: (getDefault) => getDefault().concat(api.middleware),
  });
  return render(
    <Provider store={store}>
      <VariantsTable productId={mockProducts[0].id} variants={variants} />
    </Provider>,
  );
}

describe('VariantsTable', () => {
  it('shows the current stock quantity for each variant', () => {
    resetMockProducts();
    renderTable([variant]);

    expect(screen.getByText('5')).toBeInTheDocument();
  });

  it('shows an insufficient-stock message with the available quantity on a 409', async () => {
    resetMockProducts();
    const zeroStockVariant = { ...variant, stockQuantity: 0 };
    renderTable([zeroStockVariant]);

    await userEvent.click(screen.getByText('−')); // one decrement against 0 in stock - deterministically rejected, no race on click timing

    expect(await screen.findByText(/only 0 left/i)).toBeInTheDocument();
  });
});
