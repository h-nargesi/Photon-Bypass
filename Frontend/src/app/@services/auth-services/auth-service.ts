import { Injectable } from "@angular/core";
import { Observable } from "rxjs";
import { ApiResult } from "../../@models";
import { HttpClientHandler } from "../api-services/http-client";
import { LocalStorageService } from "../local-storage/local-storage-service";

@Injectable({ providedIn: "root" })
export class AuthService {

  constructor(private readonly api: HttpClientHandler) {}

  public login(
    username: string,
    password: string
  ): Observable<ApiResult> {
    const params = { username, password };
    return this.api.authorization(`${AUTH_API_URL}/token`, params);
  }

  public logout(): Observable<ApiResult> {
    LocalStorageService.set(["user", "bearer"], undefined);
    return this.api.call(`${AUTH_API_URL}/logout`);
  }
}

const AUTH_API_URL: string = "/auth";
