import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { ApiResult, PlanType, UserPlanInfo } from '../@models';
import { ApiBaseService } from '../@services';
import { DashboardComponent } from './dashboard.component';

@Injectable({ providedIn: DashboardComponent })
export class DashboardService extends ApiBaseService {
  sendCertificateViaEmail(): Observable<ApiResult> {
    return this.call(`${ACOUNT_API_URL}/send-cert-email`);
  }

  fetchCurrentConnections(): Observable<number[]> {
    const url = `${CONNECTION_API_URL}/current-con-state`;
    return this.getData<number[]>(url);
  }

  closeConnection(index: number): Observable<ApiResult> {
    const url = `${CONNECTION_API_URL}/close-con`;
    return this.job(url, { index });
  }

  fetchPlanInfo(): Observable<UserPlanInfo> {
    const url = `${ACOUNT_API_URL}/plan-info`;
    return this.getData<UserPlanInfo>(url);
  }
}

const ACOUNT_API_URL: string = '/account';
const CONNECTION_API_URL: string = '/connection';
