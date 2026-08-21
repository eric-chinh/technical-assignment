import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import { describe, expect, it } from 'vitest';
import { api } from '../../shared/lib/apiBase';
import { CategoryListPage } from './CategoryListPage';
import { resetMockCategories } from '../../mocks/handlers';

function renderWithStore() {
  const store = configureStore({
    reducer: { [api.reducerPath]: api.reducer },
    middleware: (getDefault) => getDefault().concat(api.middleware),
  });
  return render(
    <Provider store={store}>
      <CategoryListPage />
    </Provider>,
  );
}

describe('CategoryListPage', () => {
  it('renders categories nested under their parent (tree table)', async () => {
    resetMockCategories();
    renderWithStore();

    expect(await screen.findByText('Women')).toBeInTheDocument();
    expect(screen.getByText('Dresses')).toBeInTheDocument();
  });

  it('shows a specific message when deleting a category blocked by active products (409)', async () => {
    resetMockCategories();
    renderWithStore();
    await screen.findByText('Women');

    const womenRow = screen.getByText('Women').closest('tr')!;
    await userEvent.click(within(womenRow).getByText('Delete'));
    await userEvent.click(await screen.findByRole('button', { name: /ok|yes/i }));

    expect(await screen.findByText(/still has active products/i)).toBeInTheDocument();
  });
});
