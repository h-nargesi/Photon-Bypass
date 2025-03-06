export interface TrafficDataModel {
  title: string;
  collections: TrafficData[];
  labels: string[];
}

export interface TrafficData {
  title: string;
  data: number[];
}
