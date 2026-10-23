import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResult, OvpnPasswordToken, PasswordToken } from '../@models';
import {
  ACCOUNT_API_URL,
  ApiBaseService,
  AUTH_API_URL,
  VPN_API_URL,
} from '../@services';

@Injectable({ providedIn: 'root' })
export class ChangePasswordService extends ApiBaseService {
  changePassword(model: PasswordToken): Observable<ApiResult> {
    return this.job(`${ACCOUNT_API_URL}/change-pass`, model);
  }

  /// Forgotten-password flow: the reset code + new password go to the
  /// anonymous reset endpoint (the account endpoint is [Authorize]d and
  /// answers 401 to an anonymous visitor).
  resetPassword(model: PasswordToken, headers?: Record<string, string>): Observable<ApiResult> {
    return this.job(`${AUTH_API_URL}/reset-pass`, model, undefined, headers);
  }

  changeOpenVpnPassword(model: OvpnPasswordToken): Observable<ApiResult> {
    return this.job(`${VPN_API_URL}/change-ovpn`, model);
  }
}
