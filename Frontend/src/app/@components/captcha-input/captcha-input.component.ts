import { CommonModule } from '@angular/common';
import { Component, Input, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  AlertComponent,
  ButtonDirective,
  FormControlDirective,
  SpinnerComponent,
} from '@coreui/angular';
import { IconDirective } from '@coreui/icons-angular';
import { ICON_SUBSET } from '../../@icons';
import { ApiResult, ResultStatus } from '../../@models';
import {
  CaptchaService,
  MessageService,
  TranslationPipe,
} from '../../@services';

/**
 * Single-use captcha input: image + code field + refresh. Renders itself only
 * after the host form received a 428 — the first appearance fetches a
 * challenge; every failed submit afterwards must call {@link refresh}
 * (one captcha buys exactly one attempt, server-side).
 *
 * The i18n keys live per page ({@link prefix}.captcha.*) because the texts
 * ship with the four auth page bundles.
 */
@Component({
  selector: 'app-captcha-input',
  templateUrl: './captcha-input.component.html',
  styleUrl: './captcha-input.component.scss',
  imports: [
    CommonModule,
    FormsModule,
    IconDirective,
    FormControlDirective,
    ButtonDirective,
    SpinnerComponent,
    TranslationPipe,
    AlertComponent,
  ],
})
export class CaptchaInputComponent implements OnInit {
  readonly icons = ICON_SUBSET;

  /** i18n prefix of the host page (login, register, …). */
  @Input() prefix = 'login';

  code = '';
  image?: string;
  busy = false;
  result?: ApiResult;

  readonly captcha = inject(CaptchaService);

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    if (this.busy) return;

    this.busy = true;
    this.result = undefined;

    this.captcha.fetch().subscribe({
      next: (result) => {
        this.image = result.data?.image;
        this.result = result.status() >= ResultStatus.warning ? result : undefined;
        this.busy = false;
      },
      error: () => (this.busy = false),
    });
  }

  get empty(): boolean {
    return this.code.trim().length === 0;
  }

  /** X-Captcha-* headers for the current challenge; undefined while empty. */
  headers(): Record<string, string> | undefined {
    return this.captcha.headers(this.code);
  }

  getColor(code: ResultStatus) {
    return MessageService.getStatusCode(code);
  }
}
