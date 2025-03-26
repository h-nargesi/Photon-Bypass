import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { HistoryRecord } from '../@models';
import { ACCOUNT_API_URL, ApiBaseService, ApiParam } from '../@services';
import { HistoryComponent } from './history.component';

@Injectable({ providedIn: HistoryComponent })
export class HistoryService extends ApiBaseService {
  load(
    target?: string,
    from?: number,
    to?: number
  ): Observable<HistoryRecord[]> {
    const params = { target, from, to } as ApiParam;
    return this.getData<HistoryRecord[]>(`${ACCOUNT_API_URL}/history`, params);
  }
}
