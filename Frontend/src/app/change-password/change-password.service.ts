import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResult, OvpnPasswordToken, PasswordToken } from '../@models';
import { ACCOUNT_API_URL, ApiBaseService, VPN_API_URL } from '../@services';

@Injectable({ providedIn: 'root' })
export class ChangePasswordService extends ApiBaseService {
  changePassword(model: PasswordToken): Observable<ApiResult> {
    return this.job(`${ACCOUNT_API_URL}/change-pass`, model);
  }

  changeOpenVpnPassword(model: OvpnPasswordToken): Observable<ApiResult> {
    return this.job(`${VPN_API_URL}/change-ovpn`, model);
  }
}
