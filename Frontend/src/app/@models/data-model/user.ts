export interface UserModel {
    username: string;
    fullname: string;
    email: string;
    pictureUrl?: string;
}

export interface NewUserModel {
    username: string;
    email: string;
    mobile: string;
    firstname: string;
    lastname: string;
    password: string;
}