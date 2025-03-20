import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResult, UserPlanInfo } from '../@models';
import {
  ACCOUNT_API_URL,
  ApiBaseService,
  CONNECTION_API_URL,
  PLAN_API_URL,
} from '../@services';
import { DashboardComponent } from './dashboard.component';

@Injectable({ providedIn: DashboardComponent })
export class DashboardService extends ApiBaseService {
  sendCertificateViaEmail(): Observable<ApiResult> {
    return this.call(
      `${ACCOUNT_API_URL}/send-cert-email`,
      undefined,
      undefined,
      true
    );
  }

  fetchCurrentConnections(): Observable<number[]> {
    const url = `${CONNECTION_API_URL}/current-con-state`;
    return this.getData<number[]>(url);
  }

  closeConnection(index: number): Observable<ApiResult> {
    const url = `${CONNECTION_API_URL}/close-con`;
    return this.job(url, { index }, undefined, true);
  }

  fetchPlanInfo(): Observable<UserPlanInfo> {
    const url = `${PLAN_API_URL}/plan-info`;
    return this.getData<UserPlanInfo>(url);
  }
}
