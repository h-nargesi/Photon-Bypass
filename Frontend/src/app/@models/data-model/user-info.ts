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
  targetArea?: SubUsers;
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
  password: string;
}

export interface OvpnPasswordToken extends PasswordToken, TagetAction {}

export type SubUsers = { [username: string]: Target };
