export enum PaymentMethod {
  Cash = 1,
  Card = 2
}

export interface OrderLineRequest {
  productId: string;
  quantity: number;
  discountAmount: number;
}

export interface PaymentRequest {
  method: PaymentMethod;
  amount: number;
  referenceNumber: string | null;
}

export interface CreateOrderRequest {
  lines: OrderLineRequest[];
  payments: PaymentRequest[];
}

export interface VoidOrderRequest {
  reason: string;
}

export interface OrderLineResponse {
  id: string;
  productId: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  discountAmount: number;
  taxAmount: number;
  lineTotal: number;
}

export interface PaymentResponse {
  id: string;
  method: string;
  amount: number;
  referenceNumber: string | null;
}

export interface OrderResponse {
  id: string;
  orderNumber: string;
  status: string;
  subtotal: number;
  discountAmount: number;
  taxAmount: number;
  totalAmount: number;
  changeDue: number;
  createdAtUtc: string;
  voidReason: string | null;
  voidedAtUtc: string | null;
  lines: OrderLineResponse[];
  payments: PaymentResponse[];
}
