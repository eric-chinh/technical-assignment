export interface Variant {
  id: number;
  sku: string;
  size: string | null;
  color: string | null;
  price: number;
  compareAtPrice: number | null;
  stockQuantity: number;
  barcode: string | null;
  isActive: boolean;
}

export interface Product {
  id: number;
  name: string;
  slug: string;
  description: string | null;
  categoryId: number;
  brand: string | null;
  status: string;
  attributes: string;
  imageUrl: string | null;
  variants: Variant[];
}

export interface ProductListItem {
  id: number;
  name: string;
  slug: string;
  categoryId: number;
  brand: string | null;
  status: string;
  minPrice: number | null;
  maxPrice: number | null;
  totalStock: number;
  imageUrl: string | null;
}

export interface PagedResult<T> {
  items: T[];
  nextCursor: string | null;
  hasMore: boolean;
  totalCount: number;
}

export interface CreateVariantRequest {
  sku: string;
  size: string | null;
  color: string | null;
  price: number;
  stockQuantity: number;
  compareAtPrice: number | null;
  barcode: string | null;
}

export interface CreateProductRequest {
  name: string;
  slug: string;
  categoryId: number;
  brand: string | null;
  description: string | null;
  attributes: string;
  variants: CreateVariantRequest[];
}

export interface UpdateProductRequest {
  name: string;
  description: string | null;
  categoryId: number;
  brand: string | null;
  attributes: string;
}

export interface AdjustStockResult {
  succeeded: boolean;
  newQuantity: number | null;
  availableQuantity: number | null;
}
