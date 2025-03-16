import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResult, NewUserModel } from '../@models';
import { ApiBaseService, LocalStorageService, wait } from '../@services';
import { RegisterComponent } from './register.component';

@Injectable({ providedIn: RegisterComponent })
export class RegisterService extends ApiBaseService {
  fullInfo(): Observable<NewUserModel> {
    if (LocalStorageService.UseAPI) {
      return this.getData<NewUserModel>(`${ACOUNT_API_URL}/full`);
    } else {
      return wait(
        {
          username: 'hamed@aw',
          email: 'hamed.nargesi@gmail.com',
          emailValid: false,
          mobile: '+989125157305',
          mobileValid: true,
          firstname: 'حامد',
          lastname: 'نرگسی',
          password: null as any,
        },
        2000
      );
    }
  }

  register(model: NewUserModel): Observable<ApiResult> {
    if (LocalStorageService.UseAPI) {
      return this.job(`${ACOUNT_API_URL}/register`, model);
    } else {
      return wait({ code: 200, message: 'کاربر با موفقیت ساخته شد.' }, 2000);
    }
  }
}

const ACOUNT_API_URL: string = '/account';
