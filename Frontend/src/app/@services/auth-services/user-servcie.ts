import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, of } from 'rxjs';
import { UserModel } from '../../@models';
import { ApiBaseService } from '../api-services/api-base-service';
import { HttpClientHandler } from '../api-services/http-client';
import { MessageService } from '../message-handler/message-service';
import { TranslationService } from '../translation/translation-service';

@Injectable({ providedIn: 'root' })
export class UserService extends ApiBaseService {
  private currentUser?: UserModel;
  private observableUser?: Observable<UserModel>;

  constructor(
    api: HttpClientHandler,
    router?: Router,
    message_handler?: MessageService
  ) {
    super(api, router, message_handler);
  }

  public user(): Observable<UserModel> {
    if (this.currentUser || this.currentUser === null) {
      return of(this.currentUser);
    } else if (!this.observableUser) {
      this.observableUser = this.fetchUser();
      this.observableUser.subscribe((user) => {
        this.currentUser = user;
        this.observableUser = undefined;
      });
    }

    return this.observableUser;
  }

  private fetchUser(): Observable<UserModel> {
    const url = `${AUTH_API_URL}/get-user`;
    const title = TranslationService.translate('api.recieve');
    return this.getData<UserModel>(url, title);
  }
}

const AUTH_API_URL: string = "/auth";
