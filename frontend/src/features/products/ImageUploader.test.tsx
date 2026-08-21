import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import { describe, expect, it } from 'vitest';
import { api } from '../../shared/lib/apiBase';
import { ImageUploader } from './ImageUploader';

function renderUploader(imageUrl: string | null = null) {
  const store = configureStore({
    reducer: { [api.reducerPath]: api.reducer },
    middleware: (getDefault) => getDefault().concat(api.middleware),
  });
  return render(
    <Provider store={store}>
      <ImageUploader productId={1} imageUrl={imageUrl} />
    </Provider>,
  );
}

describe('ImageUploader', () => {
  it('rejects a non-image file client-side, before any network call', async () => {
    renderUploader();
    const input = screen.getByRole('button', { name: /upload image/i }).parentElement!.querySelector('input')!;
    const badFile = new File(['x'], 'doc.pdf', { type: 'application/pdf' });

    await userEvent.upload(input, badFile);

    expect(await screen.findByText(/only jpeg, png, or webp/i)).toBeInTheDocument();
  });

  it('rejects an oversized file client-side', async () => {
    renderUploader();
    const input = screen.getByRole('button', { name: /upload image/i }).parentElement!.querySelector('input')!;
    const bigFile = new File([new Uint8Array(6 * 1024 * 1024)], 'big.jpg', { type: 'image/jpeg' });

    await userEvent.upload(input, bigFile);

    expect(await screen.findByText(/5 mb or smaller/i)).toBeInTheDocument();
  });

  it('shows the existing image, resolved against the API origin, with a Remove button when one is set', () => {
    renderUploader('/uploads/products/1/photo.jpg');

    // The backend returns a path relative to the API's own origin, not the SPA's -
    // resolveImageUrl must prefix it, or the browser resolves it against the wrong host.
    expect(screen.getByAltText('Product')).toHaveAttribute('src', 'http://localhost:8080/uploads/products/1/photo.jpg');
    expect(screen.getByText('Remove')).toBeInTheDocument();
  });
});
