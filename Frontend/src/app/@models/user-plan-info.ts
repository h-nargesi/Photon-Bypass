export interface UserPlanInfo {
  type: PlanType;
  remainsTitle: string;
  remainsPercent: number;
}

export enum PlanType {
  Monthly,
  Traffic,
}
