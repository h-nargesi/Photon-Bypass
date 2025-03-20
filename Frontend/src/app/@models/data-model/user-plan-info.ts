export interface UserPlanInfo {
  type: PlanType;
  remainsTitle: string;
  remainsPercent: number;
  simultaneousUserCount: number;
}

export enum PlanType {
  Monthly,
  Traffic,
}
