import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResult, PlanType, UserPlanInfo } from '../@models';
import { ApiBaseService, LocalStorageService, wait } from '../@services';
import { DashboardComponent } from './dashboard.component';

@Injectable({ providedIn: DashboardComponent })
export class DashboardService extends ApiBaseService {
  sendCertificateViaEmail(): Observable<ApiResult> {
    if (LocalStorageService.UseAPI) {
      return this.call(`${ACOUNT_API_URL}/send-cert-email`);
    } else {
      return wait({ code: 200, message: 'ایمیل با موفقیت ارسال شد.' }, 2000);
    }
  }

  fetchCurrentConnections(): Observable<number[]> {
    if (LocalStorageService.UseAPI) {
      const url = `${CONNECTION_API_URL}/current-con-state`;
      return this.getData<number[]>(url);
    } else {
      return wait([534, 325, 244, 16]);
    }
  }

  closeConnection(index: number): Observable<ApiResult> {
    if (LocalStorageService.UseAPI) {
      const url = `${CONNECTION_API_URL}/close-con`;
      return this.job(url, { index });
    } else {
      return wait({ code: 200, message: 'کانکشن بسته شد.' });
    }
  }

  fetchPlanInfo(): Observable<UserPlanInfo> {
    if (LocalStorageService.UseAPI) {
      const url = `${PLAN_API_URL}/plan-info`;
      return this.getData<UserPlanInfo>(url);
    } else {
      return wait({
        type: PlanType.Monthly,
        remainsTitle: '23 روز باقی مانده',
        remainsPercent: 23,
      });
    }
  }
}

const ACOUNT_API_URL: string = '/account';
const CONNECTION_API_URL: string = '/connection';
const PLAN_API_URL: string = '/plan';
