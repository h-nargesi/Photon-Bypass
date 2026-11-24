import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { HistoryRecord } from '../@models';
import { ACCOUNT_API_URL, ApiBaseService } from '../@services';

@Injectable({ providedIn: 'root' })
export class HistoryService extends ApiBaseService {
  load(
    target?: string,
    from?: number,
    to?: number
  ): Observable<HistoryRecord[]> {
    return this.getData<HistoryRecord[]>(`${ACCOUNT_API_URL}/history`, { target, from, to });
  }
}
