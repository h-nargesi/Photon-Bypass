export interface ConnectionStateModel {
  duration: number;
  state: ConnectionState;
  server: string;
  sessionId: string;
  closing: boolean;
}

export enum ConnectionState {
  Up,
  Down,
}
