export interface HistoryRecord {
  id: number;
  target: string;
  eventTime: number;
  eventTimeTitle: string;
  title: string;
  color: string;
  value?: any;
  unit?: string;
  description?: string;
}
