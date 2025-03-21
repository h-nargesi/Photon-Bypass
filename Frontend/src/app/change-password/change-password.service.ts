import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResult, OvpnPasswordToken, PasswordToken } from '../@models';
import { ACCOUNT_API_URL, ApiBaseService, AUTH_API_URL } from '../@services';
import { ChangePasswordComponent } from './change-password.component';

@Injectable({ providedIn: ChangePasswordComponent })
export class ChangePasswordService extends ApiBaseService {
  changePassword(model: PasswordToken): Observable<ApiResult> {
    return this.job(`${AUTH_API_URL}/change`, model);
  }

  changeOpenVpnPassword(model: OvpnPasswordToken): Observable<ApiResult> {
    return this.job(`${ACCOUNT_API_URL}/change-ovpn`, model);
  }
}
