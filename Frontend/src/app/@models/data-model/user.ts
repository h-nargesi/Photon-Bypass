export interface UserModel {
    username: string;
    fullname: string;
    email: string;
    pictureUrl?: string;
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
    catpcha: string;
}
