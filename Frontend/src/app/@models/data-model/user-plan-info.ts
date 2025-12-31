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
  moneyNeeds: number;
}

export interface EstimateResult {
  price: number;
  days: number;
  gigabytes: number;
  simultaneousUserCount: number;
}

export interface PaymentInvoice {
  code: number;
  items: [
    {
      title: string;
      value: number;
    }
  ];
  sum: number;
  discount: number;
  totalSum: number;
}
