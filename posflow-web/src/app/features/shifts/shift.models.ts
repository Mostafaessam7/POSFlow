export interface OpenShiftRequest {
  openingCash: number;
}

export interface CloseShiftRequest {
  closingCash: number;
}

export interface CurrentShiftResponse {
  hasOpenShift: boolean;
  shift: ShiftResponse | null;
}

export interface ShiftResponse {
  id: string;
  tenantId: string;
  branchId: string;
  userId: string;

  openingCash: number;
  closingCash: number | null;
  cashSales: number;
  expectedCash: number | null;
  cashDifference: number | null;

  openedAtUtc: string;
  closedAtUtc: string | null;

  status: 'Open' | 'Closed';
  cashierName?: string | null;
}