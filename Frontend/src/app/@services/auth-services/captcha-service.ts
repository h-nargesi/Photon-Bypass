import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { tap } from 'rxjs/operators';
import { ApiBaseService } from '../api-services/api-base-service';
import { AUTH_API_URL } from '../api-services/models/app-api-url';

export interface CaptchaResult {
  id: string;
  image: string;
  expiresInSeconds: number;
}

/**
 * Self-hosted adaptive captcha client: the image lives one attempt, so every
 * submit failure must be followed by {@link fetch} again. The current
 * challenge is shared because only one captcha-gated form is on screen at a
 * time.
 */
@Injectable({ providedIn: 'root' })
export class CaptchaService extends ApiBaseService {
  private current?: CaptchaResult;

  fetch(): Observable<CaptchaResult> {
    return this.getData<CaptchaResult>(`${AUTH_API_URL}/captcha`).pipe(
      tap(captcha => {
        this.current = captcha?.id ? captcha : undefined;
      }),
    );
  }

  /** X-Captcha-* headers for the current challenge; undefined while empty. */
  headers(code: string | undefined | null): Record<string, string> | undefined {
    const answer = code?.trim();
    if (!this.current || !answer) return undefined;

    return {
      'X-Captcha-Id': this.current.id,
      'X-Captcha-Code': answer,
    };
  }

  clear(): void {
    this.current = undefined;
  }
}
