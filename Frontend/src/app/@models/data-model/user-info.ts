export enum AccountRole {
  User = 0,
  Manager = 1,
  Admin = 2,
}

/// Panel-user status (backend `PanelUserStatus`, Docs/22 D12): only the
/// portal axis — VPN suspension is per VpnAccount.
export enum PanelUserStatus {
  Active = 0,
  Deactivated = 2,
}

/// VPN-account status (backend `VpnAccountStatus`, Docs/22 D12).
export enum VpnAccountStatus {
  Active = 0,
  Suspended = 1,
  Deactivated = 2,
}

/// One VPN account of the user (backend `UserVpnAccountModel`, Docs/22 D2).
export interface UserVpnAccount {
  id: number;
  username: string;
  realmId: number;
  realmName?: string | null;
  status: number;
  created: number;
}

export interface TagetAction {
  target?: string;
}

export interface Target {
  username: string;
  fullname: string;
  email: string;
}

export interface UserModel extends Target {
  picture?: string;
  balance: number;
  role: AccountRole;
  allowSelfService: boolean;
  targetArea?: SubUsers;
  accounts: UserVpnAccount[];
}

export interface CreateUserModel {
  username: string;
  password: string;
  email: string;
  mobile?: string;
  firstname?: string;
  lastname?: string;
  allowSelfService: boolean;
  owner?: string;
  role?: AccountRole;
}

export interface FullUserModel {
  username: string;
  email: string;
  emailValid: boolean;
  mobile: string;
  mobileValid: boolean;
  firstname: string;
  lastname: string;
}

export interface RegisterModel extends FullUserModel {
  password: string;
}

export interface PasswordToken {
  token: string;
  password?: string;
}

export interface OvpnPasswordToken extends PasswordToken, TagetAction {}

export type SubUsers = { [username: string]: Target };
