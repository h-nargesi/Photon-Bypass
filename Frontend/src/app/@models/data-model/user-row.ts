import { AccountRole, PanelUserStatus, VpnAccountStatus } from './user-info';

/// One row of the management user list (backend `UserRowModel`, Docs/20
/// phase 4 + Docs/22 §3.6): panel-user scalars with nested VPN accounts.
export interface UserRow {
  username: string;
  name?: string;
  surname?: string;
  email?: string;
  role: AccountRole;
  allowSelfService: boolean;
  status: PanelUserStatus;
  owner?: string;
  created: number;
  createdTitle: string;
  accounts: UserAccountRow[];
}

/// One VPN account nested under a user row (backend `UserAccountRowModel`).
export interface UserAccountRow {
  id: number;
  username: string;
  realmId: number;
  realmName?: string | null;
  status: VpnAccountStatus;
  created: number;
  simultaneousUser?: number;
  expirationDate?: number;
  trafficLeft?: number;
  lastConnectTime?: number;
}
