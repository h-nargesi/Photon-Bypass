import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResult } from '../@models';
import { ApiBaseService, AUTH_API_URL } from '../@services';

@Injectable({ providedIn: 'root' })
export class ForgotPasswordService extends ApiBaseService {
  reset(emailMobile: string, headers?: Record<string, string>): Observable<ApiResult> {
    return this.job(`${AUTH_API_URL}/forget-pass`, { emailMobile }, undefined, headers);
  }
}
