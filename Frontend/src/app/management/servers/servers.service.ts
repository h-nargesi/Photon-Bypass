import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResult, SaveServer, ServerRow } from '../../@models';
import { ApiBaseService, SERVER_API_URL } from '../../@services';

/// Admin server CRUD API (docs/20 phase 5, D8).
@Injectable({ providedIn: 'root' })
export class ServersService extends ApiBaseService {
  list(): Observable<ServerRow[]> {
    return this.getData<ServerRow[]>(`${SERVER_API_URL}/list`);
  }

  create(model: SaveServer): Observable<ApiResult> {
    return this.job(`${SERVER_API_URL}/create`, model);
  }

  update(model: SaveServer): Observable<ApiResult> {
    return this.job(`${SERVER_API_URL}/update`, model);
  }

  setActive(id: number, active: boolean): Observable<ApiResult> {
    return this.job(`${SERVER_API_URL}/set-active`, { id, active });
  }
}
