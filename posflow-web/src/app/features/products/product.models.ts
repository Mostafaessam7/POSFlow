export interface ProductResponse {
  id: string;
  nameAr: string;
  nameEn: string | null;
  barcode: string | null;
  price: number;
  isActive: boolean;
  categoryId: string | null;
  categoryName: string | null;
  trackStock: boolean;
  stockQuantity: number;
  rowVersion: string;
}

export interface CreateProductRequest {
  nameAr: string;
  nameEn: string | null;
  barcode: string | null;
  price: number;
  categoryId: string | null;
  trackStock: boolean;
  stockQuantity: number;
}

export interface UpdateProductRequest {
  nameAr: string;
  nameEn: string | null;
  barcode: string | null;
  price: number;
  isActive: boolean;
  categoryId: string | null;
  trackStock: boolean;
  stockQuantity: number;
  rowVersion: string;
}

export interface CategoryResponse {
  id: string;
  nameAr: string;
  nameEn: string | null;
}

export interface CreateCategoryRequest {
  nameAr: string;
  nameEn: string | null;
}
