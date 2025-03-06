import { Injectable } from '@angular/core';
import { ApiBaseService } from '../@services';
import { DashboardComponent } from './dashboard.component';
import { Observable } from 'rxjs';
import { ApiResult } from '../@models';

@Injectable({ providedIn: DashboardComponent })
export class DashboardService extends ApiBaseService {
  sendCertificateViaEmail(): Observable<ApiResult> {
    return this.call(`${DASHBOARD_API_URL}/send-cert-email`);
  }
}

const DASHBOARD_API_URL: string = '/sashboard';
