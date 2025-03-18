import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResult } from '../../@models';
import { HttpClientHandler } from '../api-services/http-client';
import { LocalStorageService } from '../local-storage/local-storage-service';
import { UserService } from './user-servcie';

@Injectable({ providedIn: 'root' })
export class AuthService {
  constructor(
    private readonly api: HttpClientHandler,
    private readonly user_service: UserService
  ) {}

  public login(username: string, password: string): Observable<ApiResult> {
    const params = { username, password };
    return this.api.authorization(`${AUTH_API_URL}/token`, params);
  }

  public check(): Observable<ApiResult> {
    return this.api.call(`${AUTH_API_URL}/check`);
  }

  public logout(): Observable<ApiResult> {
    this.user_service.clear();
    LocalStorageService.set(['user', 'bearer'], undefined);
    return this.api.call(`${AUTH_API_URL}/logout`);
  }
}

const AUTH_API_URL: string = '/auth';
