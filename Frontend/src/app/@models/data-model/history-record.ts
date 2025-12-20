export interface HistoryRecord {
  id: number;
  target: string;
  eventTime: number;
  eventTimeTitle: string;
  title: string;
  category: EventCategory;
  type: EventType;
  value?: string;
  price?: number;
  description?: string;
}

export enum EventCategory {
  Security = 1,
  Transaction = 2,
  Renewal = 3,
  Action = 4,
}

export enum EventType {
  Information = 1,
  Success = 2,
  Warning = 3,
  Error = 4,
  Critical = 5,
}
