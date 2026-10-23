import { CommonModule, NgStyle } from '@angular/common';
import { Component, OnInit, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  AlertComponent,
  ButtonDirective,
  CardBodyComponent,
  CardComponent,
  CardGroupComponent,
  ColComponent,
  ContainerComponent,
  FormControlDirective,
  FormDirective,
  FormFeedbackComponent,
  GutterDirective,
  InputGroupComponent,
  InputGroupTextDirective,
  RowComponent,
  TextColorDirective,
} from '@coreui/angular';
import { IconDirective } from '@coreui/icons-angular';
import { ICON_SUBSET } from '../@icons';
import { CaptchaInputComponent } from '../@components/captcha-input/captcha-input.component';
import { ApiResult, ResultStatus } from '../@models';
import {
  AppConfigService,
  AuthService,
  MessageService,
  TranslationPipe,
} from '../@services';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss'],
  imports: [
    CommonModule,
    FormsModule,
    NgStyle,
    RouterLink,
    ContainerComponent,
    RowComponent,
    ColComponent,
    CardGroupComponent,
    TextColorDirective,
    TranslationPipe,
    CardComponent,
    CardBodyComponent,
    FormDirective,
    InputGroupComponent,
    InputGroupTextDirective,
    IconDirective,
    FormControlDirective,
    FormFeedbackComponent,
    ButtonDirective,
    GutterDirective,
    AlertComponent,
    CaptchaInputComponent,
  ],
})
export class LoginComponent {
  readonly icons = ICON_SUBSET;
  readonly redirect: string;
  isValidated = false;
  busy = false;
  username?: string;
  password?: string;
  result?: ApiResult;

  /// Adaptive captcha: hidden until the server answers 428 (too many
  /// anonymous failures from this IP); afterwards every attempt needs a
  /// fresh single-use code.
  captcha_required = false;

  @ViewChild(CaptchaInputComponent)
  private readonly captcha_input?: CaptchaInputComponent;

  /// D11: the register CTA is only shown when the public app-config
  /// explicitly enables registration (defaults to hidden).
  allow_registration = false;

  constructor(
    private readonly service: AuthService,
    private readonly app_config: AppConfigService,
    private readonly router: Router,
    active_route: ActivatedRoute,
    auth_service: AuthService
  ) {
    this.redirect = active_route.snapshot.queryParams['redirect'] ?? '/dashboard';

    if (active_route.snapshot.routeConfig?.path === 'logout') {
      this.service.logout();
    } else {
      auth_service.check().subscribe((result) => {
        if (result?.status() === ResultStatus.success) {
          this.router.navigateByUrl(this.redirect);
        }
      });
    }
  }

  ngOnInit(): void {
    this.app_config.config().subscribe((config) => {
      this.allow_registration = config?.allowRegistration === true;
    });
  }

  login() {
    this.isValidated = true;

    if (!this.username || !this.password || this.busy) return;

    if (this.captcha_required && this.captcha_input?.empty) return;

    this.busy = true;

    this.service
      .login(this.username, this.password, this.captcha_headers())
      .subscribe((result) => {
      this.result = result;
      this.busy = false;

      if (result.code === 428) {
        this.showCaptcha();
        return;
      }

      // the captcha is single-use: every further attempt needs a fresh image
      if (this.captcha_required) this.captcha_input?.refresh();

      if (result.status() === ResultStatus.success) {
        setTimeout(() => this.router.navigateByUrl(this.redirect), 1000);
      }
    });
  }

  getColor(code: ResultStatus) {
    return MessageService.getStatusCode(code);
  }

  private showCaptcha(): void {
    if (this.captcha_required) {
      this.captcha_input?.refresh();
    } else {
      // the input appears with this change-detection cycle and fetches its
      // own first image on init
      this.captcha_required = true;
    }
  }

  private captcha_headers(): Record<string, string> | undefined {
    return this.captcha_required ? this.captcha_input?.headers() : undefined;
  }
}
