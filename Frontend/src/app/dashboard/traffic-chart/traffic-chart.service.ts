import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { TrafficDataModel } from '../../@models';
import { ApiBaseService, ApiParam, VPN_API_URL } from '../../@services';
import { TrafficChartComponent } from './traffic-chart.component';

@Injectable({ providedIn: TrafficChartComponent })
export class TrafficDataService extends ApiBaseService {
  fetchTrafficData(target?: string): Observable<TrafficDataModel> {
    const url = `${VPN_API_URL}/traffic-data`;
    return this.getData<TrafficDataModel>(url, { target } as ApiParam);
  }
}
