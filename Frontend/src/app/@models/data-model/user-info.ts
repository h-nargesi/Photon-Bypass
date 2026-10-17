export enum AccountRole {
  User = 0,
  Manager = 1,
  Admin = 2,
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
