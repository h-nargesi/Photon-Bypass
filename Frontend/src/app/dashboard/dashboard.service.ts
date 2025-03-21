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
  sendCertificateViaEmail(target?: string): Observable<ApiResult> {
    return this.call(
      `${ACCOUNT_API_URL}/send-cert-email`,
      target ? { target } : undefined,
      undefined,
      true
    );
  }

  fetchCurrentConnections(target?: string): Observable<number[]> {
    const url = `${CONNECTION_API_URL}/current-con-state`;
    return this.getData<number[]>(url, target ? { target } : undefined);
  }

  closeConnection(index: number, target?: string): Observable<ApiResult> {
    const url = `${CONNECTION_API_URL}/close-con`;
    return this.job(url, { target, index }, undefined, true);
  }

  fetchPlanInfo(target?: string): Observable<UserPlanInfo> {
    const url = `${PLAN_API_URL}/plan-info`;
    return this.getData<UserPlanInfo>(url, target ? { target } : undefined);
  }
}
