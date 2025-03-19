import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResult, PasswordToken } from '../@models';
import { ApiBaseService } from '../@services';
import { ChangePasswordComponent } from './change-password.component';

@Injectable({ providedIn: ChangePasswordComponent })
export class ChangePasswordService extends ApiBaseService {
  changePassword(model: PasswordToken): Observable<ApiResult> {
    return this.job(`${AUTH_API_URL}/change`, model);
  }

  changeOpenVpnPassword(model: PasswordToken): Observable<ApiResult> {
    return this.job(`${AUTH_API_URL}/change-ovpn`, model);
  }
}

const AUTH_API_URL: string = '/auth';
