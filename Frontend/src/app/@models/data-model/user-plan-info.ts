export interface UserPlanInfo {
  type: PlanType;
  remainsTitle: string;
  remainsPercent: number;
  simultaneousUserCount: number;
}

export interface PlanInto {
  target: string;
  type: PlanType;
  value: number;
  simultaneousUserCount: number;
}

export enum PlanType {
  Monthly,
  Traffic,
}

export interface RnewalResult {
  currentPrice: number;
  moneyNeeds: number;
}
