import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResult, NewUserModel } from '../@models';
import { ApiBaseService } from '../@services';
import { RegisterComponent } from './register.component';

@Injectable({ providedIn: RegisterComponent })
export class RegisterService extends ApiBaseService {
  fullInfo(): Observable<NewUserModel> {
    return this.getData<NewUserModel>(`${ACOUNT_API_URL}/full-info`);
  }

  register(model: NewUserModel): Observable<ApiResult> {
    return this.job(`${AUTH_API_URL}/register`, model);
  }
}

const AUTH_API_URL: string = '/account';
const ACOUNT_API_URL: string = '/account';
