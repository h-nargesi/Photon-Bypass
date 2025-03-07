import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResult } from '../../@models';
import { HttpClientHandler } from '../api-services/http-client';
import {
  LocalStorageService,
  wait,
} from '../local-storage/local-storage-service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  constructor(private readonly api: HttpClientHandler) {}

  public login(username: string, password: string): Observable<ApiResult> {
    if (LocalStorageService.UseAPI) {
      const params = { username, password };
      return this.api.authorization(`${AUTH_API_URL}/token`, params);
    } else {
      if (password !== 'password') {
        return wait({
          code: 401,
          message: 'نام کاربری یا کلمه عبور اشتباه است.',
        });
      } else {
        return wait({ code: 200, message: 'شما وارد شدید.' });
      }
    }
  }

  public logout(): Observable<ApiResult> {
    if (LocalStorageService.UseAPI) {
      LocalStorageService.set(['user', 'bearer'], undefined);
      return this.api.call(`${AUTH_API_URL}/logout`);
    } else {
      return wait({ code: 200 });
    }
  }
}

const AUTH_API_URL: string = '/auth';
