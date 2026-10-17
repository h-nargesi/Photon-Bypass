export enum BalanceDirection {
  Debit = -1,
  Credit = 1,
}

export enum BalanceStatus {
  Pending = 0,
  Completed = 1,
  Failed = 2,
  Canceled = 3,
  Verifying = 4,
}

/// One row of the wallet history (backend `WalletTransactionModel`, docs/20 phase 3).
export interface WalletTransaction {
  id: number;
  amount: number;
  direction: BalanceDirection;
  status: BalanceStatus;
  description: string;
  invoiceCode?: number;
  actor?: string;
  eventTime: number;
  eventTimeTitle: string;
}
