export interface ProductItem {
  id: number;
  sku: string;
  price: number;
  qtyInStock: number;
  productImage: string | null;
  isActive: boolean;
  version: number;
  variationOptionIds: number[];
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
  items: ProductItem[];
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

export interface CreateProductItemRequest {
  sku: string;
  price: number;
  qtyInStock: number;
  productImage: string | null;
  variationOptionIds: number[];
}

export interface CreateProductRequest {
  name: string;
  slug: string;
  categoryId: number;
  brand: string | null;
  description: string | null;
  attributes: string;
  items: CreateProductItemRequest[];
}

export interface UpdateProductRequest {
  name: string;
  description: string | null;
  categoryId: number;
  brand: string | null;
  attributes: string;
}

export interface UpdateProductItemRequest {
  price: number;
  productImage: string | null;
}

export interface AdjustStockResult {
  succeeded: boolean;
  newQuantity: number | null;
  availableQuantity: number | null;
}

export interface VariationOption {
  id: number;
  variationId: number;
  value: string;
}

export interface Variation {
  id: number;
  categoryId: number;
  name: string;
  options: VariationOption[];
}

export interface PromotionCategory {
  promotionId: number;
  categoryId: number;
}

export interface Promotion {
  id: number;
  name: string;
  description: string | null;
  discountRate: number;
  startDate: string;
  endDate: string;
  categories: PromotionCategory[];
}
