import { AccountRole } from './user-info';

/// One row of the management user list (backend `UserRowModel`, docs/20 phase 4).
export interface UserRow {
  username: string;
  name?: string;
  surname?: string;
  email?: string;
  role: AccountRole;
  allowSelfService: boolean;
  isActive: boolean;
  owner?: string;
  created: number;
  createdTitle: string;
  simultaneousUser?: number;
  expirationDate?: number;
  trafficLeft?: number;
  lastConnectTime?: number;
}
