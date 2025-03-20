export interface UserModel {
  username: string;
  fullname: string;
  email: string;
  picture?: string;
  balance: number;
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
