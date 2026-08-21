export interface Category {
  id: number;
  name: string;
  slug: string;
  parentCategoryId: number | null;
  displayOrder: number;
  isActive: boolean;
}

export interface CreateCategoryRequest {
  name: string;
  slug: string;
  parentCategoryId: number | null;
  displayOrder: number;
}

export interface UpdateCategoryRequest extends CreateCategoryRequest {
  isActive: boolean;
}
