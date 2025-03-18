import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { TrafficDataModel } from '../../@models';
import { ApiBaseService } from '../../@services';
import { TrafficChartComponent } from './traffic-chart.component';

@Injectable({ providedIn: TrafficChartComponent })
export class TrafficDataService extends ApiBaseService {
  fetchTrafficData(): Observable<TrafficDataModel> {
    const url = `${ACOUNT_API_URL}/traffic-data`;
    return this.getData<TrafficDataModel>(url);
  }
}

const ACOUNT_API_URL: string = '/account';
