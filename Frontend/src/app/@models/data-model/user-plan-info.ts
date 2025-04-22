export interface UserPlanInfo {
  type: PlanType;
  remainsTitle: string;
  remainsPercent: number;
  simultaneousUserCount: number;
}

export interface PlanEstimate {
  type: PlanType;
  value: number;
  simultaneousUserCount: number;
}

export interface PlanInfo extends PlanEstimate {
  target: string;
}

export enum PlanType {
  Monthly,
  Traffic,
}

export interface RenewalResult {
  currentPrice: number;
  moneyNeeds: number;
}

export interface PaymentInvoice {
  code: string;
  items: [
    {
      title: string;
      value: number;
    }
  ];
  sum: number;
  tax: number;
  totalSum: number;
}
