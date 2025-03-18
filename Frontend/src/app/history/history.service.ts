import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { HistoryRecord } from '../@models';
import { ApiBaseService } from '../@services';
import { HistoryComponent } from './history.component';

@Injectable({ providedIn: HistoryComponent })
export class HistoryService extends ApiBaseService {
  load(from?: number, to?: number): Observable<HistoryRecord[]> {
    return this.getData<HistoryRecord[]>(
      `${ACOUNT_API_URL}/history`,
      this.getApiParam({ from, to })
    );
  }
}

const ACOUNT_API_URL: string = '/account';
