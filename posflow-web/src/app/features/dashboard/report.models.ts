export interface TopProductResponse {
  productName: string;
  quantitySold: number;
  revenue: number;
}

export interface DailySummaryResponse {
  date: string;
  orderCount: number;
  voidedOrderCount: number;
  totalSales: number;
  averageTicket: number;
  cashSales: number;
  cardSales: number;
  topProducts: TopProductResponse[];
}
