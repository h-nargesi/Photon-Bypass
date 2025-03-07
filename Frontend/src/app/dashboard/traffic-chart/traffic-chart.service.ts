import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { TrafficData, TrafficDataModel } from '../../@models';
import { ApiBaseService, LocalStorageService, wait } from '../../@services';
import { TrafficChartComponent } from './traffic-chart.component';

@Injectable({ providedIn: TrafficChartComponent })
export class TrafficDataService extends ApiBaseService {
  fetchTrafficData(): Observable<TrafficDataModel> {
    if (LocalStorageService.UseAPI) {
      const url = `${ACOUNT_API_URL}/traffic-data`;
      return this.getData<TrafficDataModel>(url);
    } else {
      const data: TrafficDataModel = {
        title: 'ترافیک یک ماه گذشته',
        collections: [],
        labels: [
          'January',
          'February',
          'March',
          'April',
          'May',
          'June',
          'July',
          'August',
          'September',
          'October',
          'November',
          'December',
        ],
      };

      const upload: TrafficData = {
        title: 'Upload',
        data: [],
      };
      for (let i = 0; i < data.labels.length; i++) {
        upload.data.push(this.random(50, 240));
      }

      const download: TrafficData = {
        title: 'Download',
        data: [],
      };
      for (let i = 0; i < data.labels.length; i++) {
        download.data.push(this.random(20, 160));
      }

      const total: TrafficData = {
        title: 'Total',
        data: [],
      };
      let sum: number = 0;
      for (let i = 0; i < data.labels.length; i++) {
        sum += upload.data[i] + download.data[i];
        total.data.push(upload.data[i] + download.data[i]);
      }

      const average: TrafficData = {
        title: 'Average',
        data: [],
      };
      sum = sum / data.labels.length;
      for (let i = 0; i < data.labels.length; i++) {
        average.data.push(sum);
      }

      data.collections.push(upload);
      data.collections.push(download);
      data.collections.push(total);
      data.collections.push(average);

      return wait(data, 2000);
    }
  }
  
  private random(min: number, max: number) {
    return Math.floor(Math.random() * (max - min + 1) + min);
  }
}

const ACOUNT_API_URL: string = '/account';
