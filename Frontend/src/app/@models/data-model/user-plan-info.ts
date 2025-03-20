export interface UserPlanInfo {
  type: PlanType;
  remainsTitle: string;
  remainsPercent: number;
  simultaneousUserCount: number;
}

export interface PlanInto {
  type: PlanType;
  value: number;
  simultaneousUserCount: number;
}

export enum PlanType {
  Monthly,
  Traffic,
}
