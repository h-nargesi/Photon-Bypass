import { Injectable } from "@angular/core";
import { Observable } from "rxjs";
import { ApiResult } from "../../@models";
import { HttpClientHandler } from "../api-services/http-client";
import { LocalStorageService } from "../local-storage/local-storage-service";

@Injectable({ providedIn: "root" })
export class AuthService {
  private app_path: string = "/connect";

  constructor(private readonly api: HttpClientHandler) {}

  public login(
    username: string,
    password: string
  ): Observable<ApiResult<boolean>> {
    const params = { username, password };
    return this.api.authorization<boolean>(`${this.app_path}/token`, params);
  }

  public logout(): Observable<ApiResult<boolean>> {
    LocalStorageService.set(["user", "bearer"], undefined);
    return this.api.get<boolean>(`${this.app_path}/logout`);
  }
}
