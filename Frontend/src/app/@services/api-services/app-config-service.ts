import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { AppConfig, ShowMessageCase } from '../../@models';
import { BASICS_API_URL } from './models/app-api-url';
import { ApiBaseService } from './api-base-service';

/// Public app configuration (docs/20 D11) — unauthenticated, mirrors only
/// the flags the pre-login UI needs. Failures stay silent: the register
/// link simply stays hidden when the flag cannot be confirmed.
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
