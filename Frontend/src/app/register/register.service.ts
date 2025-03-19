import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResult, FullUserModel, RegisterModel } from '../@models';
import { ApiBaseService } from '../@services';
import { RegisterComponent } from './register.component';

@Injectable({ providedIn: RegisterComponent })
export class RegisterService extends ApiBaseService {
  fullInfo(): Observable<FullUserModel> {
    return this.getData<FullUserModel>(`${ACOUNT_API_URL}/full-info`);
  }

  edit(model: FullUserModel): Observable<ApiResult> {
    return this.job(`${ACOUNT_API_URL}/edit-user`, model);
  }

  register(model: RegisterModel): Observable<ApiResult> {
    return this.job(`${AUTH_API_URL}/register`, model);
  }
}

const AUTH_API_URL: string = '/auth';
const ACOUNT_API_URL: string = '/account';
