import { describe, expect, it } from 'vitest';
import { buildCategoryTree } from './buildCategoryTree';
import type { Category } from './types';

const women: Category = { id: 1, name: 'Women', slug: 'women', parentCategoryId: null, displayOrder: 0, isActive: true };
const dresses: Category = { id: 2, name: 'Dresses', slug: 'dresses', parentCategoryId: 1, displayOrder: 0, isActive: true };
const maxiDresses: Category = { id: 3, name: 'Maxi Dresses', slug: 'maxi-dresses', parentCategoryId: 2, displayOrder: 0, isActive: true };

describe('buildCategoryTree', () => {
  it('nests children under their parent, three levels deep', () => {
    const tree = buildCategoryTree([women, dresses, maxiDresses]);

    expect(tree).toHaveLength(1);
    expect(tree[0].id).toBe(1);
    expect(tree[0].children?.[0].id).toBe(2);
    expect(tree[0].children?.[0].children?.[0].id).toBe(3);
  });

  it('puts a category with a missing parent reference at the top level instead of dropping it', () => {
    const orphan: Category = { id: 4, name: 'Orphan', slug: 'orphan', parentCategoryId: 999, displayOrder: 0, isActive: true };

    const tree = buildCategoryTree([orphan]);

    expect(tree).toHaveLength(1);
    expect(tree[0].id).toBe(4);
  });
});
