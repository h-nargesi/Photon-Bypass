import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, of, tap } from 'rxjs';
import { UserModel } from '../../@models';
import { ApiBaseService } from '../api-services/api-base-service';
import { HttpClientHandler } from '../api-services/http-client';
import { MessageService } from '../message-handler/message-service';
import { TranslationService } from '../translation/translation-service';

@Injectable({ providedIn: 'root' })
export class UserService extends ApiBaseService {
  private current_user?: UserModel;
  private observable_user?: Observable<UserModel>;

  constructor(
    api: HttpClientHandler,
    router?: Router,
    message_handler?: MessageService
  ) {
    super(api, router, message_handler);
  }

  public user(): Observable<UserModel> {
    if (this.current_user || this.current_user === null) {
      return of(this.current_user);
    } else if (!this.observable_user) {
      this.observable_user = this.fetchUser();
      this.observable_user.pipe(
        tap((user) => {
          this.current_user = user;
          this.observable_user = undefined;
        })
      );
    }

    return this.observable_user;
  }

  private fetchUser(): Observable<UserModel> {
    const url = `${AUTH_API_URL}/get-user`;
    const title = TranslationService.translate('api.recieve');
    return this.getData<UserModel>(url, title);
  }
}

const AUTH_API_URL: string = '/auth';
