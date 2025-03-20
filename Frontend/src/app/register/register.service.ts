import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResult, FullUserModel, RegisterModel } from '../@models';
import { ACCOUNT_API_URL, ApiBaseService, AUTH_API_URL } from '../@services';
import { RegisterComponent } from './register.component';

@Injectable({ providedIn: RegisterComponent })
export class RegisterService extends ApiBaseService {
  fullInfo(): Observable<FullUserModel> {
    return this.getData<FullUserModel>(`${ACCOUNT_API_URL}/full-info`);
  }

  edit(model: FullUserModel): Observable<ApiResult> {
    return this.job(`${ACCOUNT_API_URL}/edit-user`, model);
  }

  register(model: RegisterModel): Observable<ApiResult> {
    return this.job(`${AUTH_API_URL}/register`, model);
  }
}
