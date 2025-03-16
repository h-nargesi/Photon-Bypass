export interface UserModel {
    username: string;
    fullname: string;
    email: string;
    pictureUrl?: string;
}

export interface NewUserModel {
    username: string;
    email: string;
    emailValid: boolean;
    mobile: string;
    mobileValid: boolean;
    firstname: string;
    lastname: string;
    password: string;
}