import { HttpResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ApiResult,
  ConnectionStateModel,
  PlanInfo,
  ShowMessageCase,
  PlanState,
} from '../@models';
import {
  ApiBaseService,
  ApiParam,
  CONNECTION_API_URL,
  PLAN_API_URL,
  VPN_API_URL,
} from '../@services';

@Injectable({ providedIn: 'root' })
export class DashboardService extends ApiBaseService {
  sendCertificateViaEmail(target?: string): Observable<ApiResult> {
    return this.call(`${VPN_API_URL}/send-cert-email`, target ? { target } as ApiParam : undefined, {
      show_message: ShowMessageCase.success,
    });
  }

  downloadCertificate(target?: string): Observable<HttpResponse<Blob>> {
    return this.api.download(
      `${VPN_API_URL}/download-cert`,
      target ? { target } as ApiParam : undefined,
    );
  }

  fetchCurrentConnections(target?: string): Observable<ConnectionStateModel[]> {
    const url = `${CONNECTION_API_URL}/current-con-state`;
    return this.getData<ConnectionStateModel[]>(url, target ? { target } as ApiParam : undefined);
  }

  closeConnection(
    server: string,
    sessionId: string,
    target?: string
  ): Observable<ApiResult> {
    const url = `${CONNECTION_API_URL}/close-con`;
    return this.job(url, { server, target: target ?? null, sessionId });
  }

  fetchPlanState(target?: string): Observable<PlanState> {
    const url = `${PLAN_API_URL}/plan-state`;
    return this.getData<PlanState>(url, target ? { target } as ApiParam : undefined);
  }

  fetchPlanInfo(target?: string): Observable<PlanInfo> {
    const url = `${PLAN_API_URL}/plan-info`;
    return this.getData<PlanInfo>(url, target ? { target } as ApiParam : undefined);
  }
}
