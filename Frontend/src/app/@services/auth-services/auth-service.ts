import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResult, ShowMessageCase } from '../../@models';
import { ApiBaseService } from '../api-services/api-base-service';
import { HttpClientHandler } from '../api-services/http-client';
import { AUTH_API_URL } from '../api-services/models/app-api-url';
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

  public login(username: string, password: string): Observable<ApiResult> {
    const params = { username, password };
    return this.authorization(`${AUTH_API_URL}/token`, params);
  }

  public check(): Observable<ApiResult> {
    return this.call(`${AUTH_API_URL}/check`, undefined, {
      show_message: ShowMessageCase.silence,
    });
  }

  public logout(): Observable<ApiResult> {
    this.user_service.clear();
    LocalStorageService.set(['user', 'bearer'], undefined);
    return this.call(`${AUTH_API_URL}/logout`, undefined, {
      show_message: ShowMessageCase.silence,
    });
  }
}
