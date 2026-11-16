import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { TrafficDataModel } from '../../@models';
import { ApiBaseService, ApiParam, VPN_API_URL } from '../../@services';

@Injectable({ providedIn: 'root' })
export class TrafficDataService extends ApiBaseService {
  fetchTrafficData(accountId?: number): Observable<TrafficDataModel> {
    const url = `${VPN_API_URL}/traffic-data`;
    return this.getData<TrafficDataModel>(url, accountId ? { accountId } as ApiParam : undefined);
  }
}
