import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { UserModel } from '../../@models';
import { ApiBaseService } from '../api-services/api-base-service';
import { HttpClientHandler } from '../api-services/http-client';
import { MessageService } from '../message-handler/message-service';

@Injectable({ providedIn: 'root' })
export class UserService extends ApiBaseService {
  private app_path: string = '/user';

  constructor(
    api: HttpClientHandler,
    router?: Router,
    message_handler?: MessageService
  ) {
    super(api, router, message_handler);
  }

  public user(show_error?: boolean, title?: string): Observable<UserModel | undefined> {
    const url = `${this.app_path}/get`;
    return this.getData<UserModel>(url, title, undefined, show_error);
  }
}
