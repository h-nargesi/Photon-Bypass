import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ApiResult,
  FullUserModel,
  RegisterModel,
  TelegramLinkModel,
} from '../@models';
import {
  ACCOUNT_API_URL,
  ApiBaseService,
  ApiParam,
  AUTH_API_URL,
} from '../@services';

@Injectable({ providedIn: 'root' })
export class RegisterService extends ApiBaseService {
  fullInfo(target?: string): Observable<FullUserModel> {
    const param = target ? { target } as ApiParam : undefined;
    return this.getData<FullUserModel>(`${ACCOUNT_API_URL}/full-info`, param);
  }

  edit(model: FullUserModel, target?: string): Observable<ApiResult> {
    return this.job(`${ACCOUNT_API_URL}/edit-user?target=${target ?? ''}`, model);
  }

  register(model: RegisterModel, headers?: Record<string, string>): Observable<ApiResult> {
    return this.job(`${AUTH_API_URL}/register`, model, undefined, headers);
  }

  telegramLinkStatus(target?: string): Observable<TelegramLinkModel> {
    const param = target ? { target } as ApiParam : undefined;
    return this.getData<TelegramLinkModel>(`${ACCOUNT_API_URL}/telegram-link`, param);
  }

  createTelegramLink(target?: string): Observable<TelegramLinkModel> {
    return this.postData<TelegramLinkModel>(
      `${ACCOUNT_API_URL}/telegram-link?target=${target ?? ''}`,
      null,
    );
  }

  disconnectTelegram(target?: string): Observable<ApiResult> {
    const param = target ? { target } as ApiParam : undefined;
    return this.remove(`${ACCOUNT_API_URL}/telegram-link`, param);
  }
}
