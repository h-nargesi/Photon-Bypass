import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { TrafficDataModel } from '../../@models';
import { ACCOUNT_API_URL, ApiBaseService } from '../../@services';
import { TrafficChartComponent } from './traffic-chart.component';

@Injectable({ providedIn: TrafficChartComponent })
export class TrafficDataService extends ApiBaseService {
  fetchTrafficData(): Observable<TrafficDataModel> {
    const url = `${ACCOUNT_API_URL}/traffic-data`;
    return this.getData<TrafficDataModel>(url);
  }
}
