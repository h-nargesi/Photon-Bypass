import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResult, ShowMessageCase, UserPlanInfo } from '../@models';
import {
  ApiBaseService,
  ApiParam,
  CONNECTION_API_URL,
  PLAN_API_URL,
  VPN_API_URL,
} from '../@services';
import { DashboardComponent } from './dashboard.component';

@Injectable({ providedIn: DashboardComponent })
export class DashboardService extends ApiBaseService {
  sendCertificateViaEmail(target?: string): Observable<ApiResult> {
    return this.call(`${VPN_API_URL}/send-cert-email`, { target } as ApiParam, {
      show_message: ShowMessageCase.success,
    });
  }

  fetchCurrentConnections(target?: string): Observable<number[]> {
    const url = `${CONNECTION_API_URL}/current-con-state`;
    return this.getData<number[]>(url, { target } as ApiParam);
  }

  closeConnection(index: number, target?: string): Observable<ApiResult> {
    const url = `${CONNECTION_API_URL}/close-con`;
    return this.job(url, { target, index });
  }

  fetchPlanState(target?: string): Observable<UserPlanInfo> {
    const url = `${PLAN_API_URL}/plan-state`;
    return this.getData<UserPlanInfo>(url, { target } as ApiParam);
  }
}
