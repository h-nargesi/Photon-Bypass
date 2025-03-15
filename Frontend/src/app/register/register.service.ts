import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResult, NewUserModel } from '../@models';
import { ApiBaseService, LocalStorageService, wait } from '../@services';
import { RegisterComponent } from './register.component';

@Injectable({ providedIn: RegisterComponent })
export class RegisterService extends ApiBaseService {
  register(model: NewUserModel): Observable<ApiResult> {
    if (LocalStorageService.UseAPI) {
      return this.job(`${ACOUNT_API_URL}/register`, model);
    } else {
      return wait({ code: 200, message: 'کاربر با موفقیت ساخته شد.' }, 2000);
    }
  }
}

const ACOUNT_API_URL: string = '/account';
