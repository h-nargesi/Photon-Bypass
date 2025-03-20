import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { HistoryRecord } from '../@models';
import { ACCOUNT_API_URL, ApiBaseService } from '../@services';
import { HistoryComponent } from './history.component';

@Injectable({ providedIn: HistoryComponent })
export class HistoryService extends ApiBaseService {
  load(from?: number, to?: number): Observable<HistoryRecord[]> {
    return this.getData<HistoryRecord[]>(
      `${ACCOUNT_API_URL}/history`,
      this.getApiParam({ from, to })
    );
  }
}
