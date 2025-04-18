import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResult, FullUserModel, RegisterModel } from '../@models';
import {
  ACCOUNT_API_URL,
  ApiBaseService,
  ApiParam,
  AUTH_API_URL,
} from '../@services';
import { RegisterComponent } from './register.component';

@Injectable({ providedIn: RegisterComponent })
export class RegisterService extends ApiBaseService {
  fullInfo(target?: string): Observable<FullUserModel> {
    const param = { target } as ApiParam;
    return this.getData<FullUserModel>(`${ACCOUNT_API_URL}/full-info`, param);
  }

  edit(model: FullUserModel, target?: string): Observable<ApiResult> {
    return this.job(`${ACCOUNT_API_URL}/edit-user?target=${target ?? ''}`, model);
  }

  register(model: RegisterModel): Observable<ApiResult> {
    return this.job(`${AUTH_API_URL}/register`, model);
  }
}
