import type { Category } from './types';

export interface CategoryTreeNode extends Category {
  children?: CategoryTreeNode[];
}

export function buildCategoryTree(categories: Category[]): CategoryTreeNode[] {
  const byId = new Map<number, CategoryTreeNode>(categories.map((c) => [c.id, { ...c }]));
  const roots: CategoryTreeNode[] = [];

  for (const category of byId.values()) {
    const parent = category.parentCategoryId !== null ? byId.get(category.parentCategoryId) : undefined;
    if (parent) {
      parent.children = parent.children ?? [];
      parent.children.push(category);
    } else {
      roots.push(category); // top-level, or an orphaned reference - shown rather than silently dropped
    }
  }

  return roots;
}
