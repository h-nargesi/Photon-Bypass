import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResult } from '../../@models';
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
    return this.authorization(`${AUTH_API_URL}/token`, params, undefined, headers);
  }

  public override check(): Observable<ApiResult> {
    return super.check(`${ACCOUNT_API_URL}/get-user`);
  }

  public logout(): void {
    this.user_service.clear();
    LocalStorageService.set(['user', 'bearer'], undefined);
    ApiBaseService.is_logout = false;
  }
}
