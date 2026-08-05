import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import { PagedResult } from '../../shared/paged-result.model';

import {
  CategoryResponse,
  CreateCategoryRequest,
  CreateProductRequest,
  ProductResponse,
  UpdateProductRequest
} from './product.models';

@Injectable({
  providedIn: 'root'
})
export class ProductService {
  private readonly http = inject(HttpClient);

  getAll(
    includeInactive = false,
    categoryId: string | null = null,
    page = 1,
    pageSize = 50
  ): Observable<PagedResult<ProductResponse>> {
    let params = new HttpParams()
      .set('includeInactive', includeInactive)
      .set('page', page)
      .set('pageSize', pageSize);

    if (categoryId) {
      params = params.set('categoryId', categoryId);
    }

    return this.http.get<PagedResult<ProductResponse>>(
      '/api/products',
      { params }
    );
  }

  create(
    request: CreateProductRequest
  ): Observable<ProductResponse> {
    return this.http.post<ProductResponse>(
      '/api/products',
      request
    );
  }

  update(
    id: string,
    request: UpdateProductRequest
  ): Observable<ProductResponse> {
    return this.http.put<ProductResponse>(
      `/api/products/${id}`,
      request
    );
  }

  deactivate(id: string): Observable<void> {
    return this.http.delete<void>(
      `/api/products/${id}`
    );
  }

  getCategories(): Observable<CategoryResponse[]> {
    return this.http.get<CategoryResponse[]>(
      '/api/categories'
    );
  }

  createCategory(
    request: CreateCategoryRequest
  ): Observable<CategoryResponse> {
    return this.http.post<CategoryResponse>(
      '/api/categories',
      request
    );
  }
}
