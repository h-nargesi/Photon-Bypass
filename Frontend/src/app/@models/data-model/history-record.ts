export interface HistoryRecord {
    id: number;
    eventTime: number;
    eventTimeTitle: string;
    title: string;
    color: string;
    value?: any;
    unit?: string;
    description?: string;
}