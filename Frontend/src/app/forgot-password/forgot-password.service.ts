import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResult } from '../@models';
import { ApiBaseService, AUTH_API_URL } from '../@services';
import { ForgotPasswordComponent } from './forgot-password.component';

@Injectable({ providedIn: ForgotPasswordComponent })
export class ForgotPasswordService extends ApiBaseService {
  reset(emailMobile: string): Observable<ApiResult> {
    return this.job(`${AUTH_API_URL}/reset`, { emailMobile });
  }
}
