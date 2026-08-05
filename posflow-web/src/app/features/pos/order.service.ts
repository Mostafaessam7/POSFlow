import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import { PagedResult } from '../../shared/paged-result.model';

import {
  CreateOrderRequest,
  OrderResponse,
  VoidOrderRequest
} from './order.models';

@Injectable({
  providedIn: 'root'
})
export class OrderService {
  private readonly http = inject(HttpClient);

  checkout(
    request: CreateOrderRequest
  ): Observable<OrderResponse> {
    return this.http.post<OrderResponse>(
      '/api/orders/checkout',
      request
    );
  }

  getCurrentShiftOrders(
    page = 1,
    pageSize = 50
  ): Observable<PagedResult<OrderResponse>> {
    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);

    return this.http.get<PagedResult<OrderResponse>>(
      '/api/orders',
      { params }
    );
  }

  getByShiftId(
    shiftId: string,
    page = 1,
    pageSize = 50
  ): Observable<PagedResult<OrderResponse>> {
    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);

    return this.http.get<PagedResult<OrderResponse>>(
      `/api/orders/by-shift/${shiftId}`,
      { params }
    );
  }

  voidOrder(
    id: string,
    request: VoidOrderRequest
  ): Observable<OrderResponse> {
    return this.http.post<OrderResponse>(
      `/api/orders/${id}/void`,
      request
    );
  }
}
