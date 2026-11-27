import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ApiResult,
  ApiResultData,
  SaveServer,
  ServerConnectionReport,
  ServerConnectionTest,
  ServerRow,
  ShowMessageCase,
} from '../../@models';
import { ApiBaseService, SERVER_API_URL, TranslationService } from '../../@services';

/// Admin server CRUD API (Docs/20 phase 5, D8) and its connection test (Docs/25 D8).
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

  /// Tests a server without saving anything; only a refused request is toasted, the report is rendered by the caller.
  testConnection(model: ServerConnectionTest): Observable<ApiResultData<ServerConnectionReport>> {
    return this.post<ServerConnectionReport>(`${SERVER_API_URL}/test-connection`, model, {
      title: TranslationService.translate('management.servers.modals.test-title'),
      show_message: ShowMessageCase.errors,
    });
  }
}
