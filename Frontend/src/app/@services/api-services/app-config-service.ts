import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { AppConfig, ShowMessageCase } from '../../@models';
import { BASICS_API_URL } from './models/app-api-url';
import { ApiBaseService } from './api-base-service';

/// Public app configuration (Docs/20 D11) — unauthenticated, carries only
/// the flags and public texts the UI needs. Failures stay silent: the register
/// link simply stays hidden and the support card shows no contact line when
/// the config cannot be confirmed.
@Injectable({ providedIn: 'root' })
export class AppConfigService extends ApiBaseService {
  config(): Observable<AppConfig | null> {
    return this.getData<AppConfig>(
      `${BASICS_API_URL}/app-config`,
      undefined,
      { show_message: ShowMessageCase.silence },
    );
  }
}
