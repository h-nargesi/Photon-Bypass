export interface PlanState {
  remainsTitle: string;
  remainsTrafficPercent: number;
  remainsTimePercent: number;
  simultaneousUserCount: number;
}

export interface RenewalContext {
  target: string;
  days: number;
  gigabytes: number;
  simultaneousUserCount: number;
}

export interface PlanInfo {
  target: string;
  days: number;
  gigabytes: number;
  simultaneousUserCount: number;
}

export interface RenewalResult {
  currentPrice: number;
  invoiceCode?: number;
}

export interface EstimateResult {
  price: number;
  days: number;
  gigabytes: number;
  simultaneousUserCount: number;
}

export interface PaymentInvoiceItem {
  title: string;
  value: number;
}

export interface PaymentCard {
  bankName?: string;
  cardNumber?: string;
  holderName?: string;
}

export interface PaymentInvoice {
  code: number;
  kind: number;
  status: number;
  totalPrice: number;
  walletDeduction: number;
  payable: number;
  walletBalance: number;
  allowWallet: boolean;
  hasReceipt: boolean;
  items: PaymentInvoiceItem[];
  cardInfo: PaymentCard[];
}
