import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { tap } from 'rxjs/operators';
import { ApiResult, ShowMessageCase } from '../../@models';
import { ApiBaseService } from '../api-services/api-base-service';
import { HttpClientHandler } from '../api-services/http-client';
import {
  ACCOUNT_API_URL,
  AUTH_API_URL,
} from '../api-services/models/app-api-url';
import { LocalStorageService } from '../local-storage/local-storage-service';
import { MessageService } from '../message-handler/message-service';
import { UserService } from './user-servcie';

@Injectable({ providedIn: 'root' })
export class AuthService extends ApiBaseService {
  constructor(
    private readonly user_service: UserService,
    api: HttpClientHandler,
    message_handler?: MessageService
  ) {
    super(api, undefined, message_handler);
  }

  public login(
    username: string,
    password: string,
    headers?: Record<string, string>
  ): Observable<ApiResult> {
    const params = { username, password };
    // D4: the login page shows its answers inline — the success message is never a toast
    return this.authorization(
      `${AUTH_API_URL}/token`,
      params,
      { show_message: ShowMessageCase.errors },
      headers
    );
  }

  /// Session probe of the login page — only called with a stored session. A
  /// rejected session is the expected outcome there, not an error: nothing is
  /// shown and the dead session is closed.
  public override check(): Observable<ApiResult> {
    return super
      .check(`${ACCOUNT_API_URL}/get-user`, undefined, {
        show_message: ShowMessageCase.silence,
      })
      .pipe(tap((result) => { if (result.code === 401) this.closeSession(); }));
  }

  public logout(): void {
    this.closeSession();
    ApiBaseService.is_logout = false;
  }

  /// Client-side anonymous state: cached profile, stored token, target and account.
  public closeSession(): void {
    this.user_service.clear();
    LocalStorageService.set(['user', 'bearer'], undefined);
  }
}
