import { describe, expect, it } from 'vitest';
import { resolveImageUrl } from './resolveImageUrl';

describe('resolveImageUrl', () => {
  it('prefixes a relative backend path with the API origin', () => {
    expect(resolveImageUrl('/uploads/products/5000/photo.jpg')).toBe(
      'http://localhost:8080/uploads/products/5000/photo.jpg',
    );
  });

  it('leaves an already-absolute URL unchanged', () => {
    expect(resolveImageUrl('https://cdn.example.com/photo.jpg')).toBe('https://cdn.example.com/photo.jpg');
  });

  it('returns null for a null input', () => {
    expect(resolveImageUrl(null)).toBeNull();
  });
});
