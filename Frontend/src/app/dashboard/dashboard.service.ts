import { HttpResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ApiResult,
  ConnectionStateModel,
  PlanInfo,
  ShowMessageCase,
  PlanState,
  VpnConfigInfo,
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
  sendCertificateViaEmail(accountId?: number): Observable<ApiResult> {
    return this.call(`${VPN_API_URL}/send-cert-email`, accountId ? { accountId } as ApiParam : undefined, {
      show_message: ShowMessageCase.success,
    });
  }

  downloadCertificate(accountId?: number): Observable<HttpResponse<Blob>> {
    return this.api.download(
      `${VPN_API_URL}/download`,
      accountId ? { accountId } as ApiParam : undefined,
    );
  }

  fetchConfigInfo(accountId?: number): Observable<VpnConfigInfo> {
    return this.getData<VpnConfigInfo>(
      `${VPN_API_URL}/config-info`,
      accountId ? { accountId } as ApiParam : undefined,
    );
  }

  fetchCurrentConnections(accountId?: number): Observable<ConnectionStateModel[]> {
    const url = `${CONNECTION_API_URL}/current-con-state`;
    return this.getData<ConnectionStateModel[]>(url, accountId ? { accountId } as ApiParam : undefined);
  }

  closeConnection(
    server: string,
    sessionId: string,
    accountId?: number
  ): Observable<ApiResult> {
    const url = `${CONNECTION_API_URL}/close-con`;
    return this.job(url, { server, accountId: accountId ?? null, sessionId });
  }

  fetchPlanState(accountId?: number): Observable<PlanState> {
    const url = `${PLAN_API_URL}/plan-state`;
    return this.getData<PlanState>(url, accountId ? { accountId } as ApiParam : undefined);
  }

  fetchPlanInfo(accountId?: number): Observable<PlanInfo> {
    const url = `${PLAN_API_URL}/plan-info`;
    return this.getData<PlanInfo>(url, accountId ? { accountId } as ApiParam : undefined);
  }
}
