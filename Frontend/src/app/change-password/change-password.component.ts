import { CommonModule } from '@angular/common';
import { Component, ViewChild } from '@angular/core';
import {
  FormBuilder,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  AlertComponent,
  ButtonDirective,
  CardBodyComponent,
  CardComponent,
  ColComponent,
  ContainerComponent,
  FormControlDirective,
  FormDirective,
  FormFeedbackComponent,
  InputGroupComponent,
  InputGroupTextDirective,
  RowComponent,
  TextColorDirective,
} from '@coreui/angular';
import { IconDirective } from '@coreui/icons-angular';
import { ICON_SUBSET } from '../@icons';
import { CaptchaInputComponent } from '../@components/captcha-input/captcha-input.component';
import {
  ApiResult,
  OvpnPasswordToken,
  PasswordToken,
  ResultStatus,
} from '../@models';
import { PasswordValidators, TranslationPipe, UserService } from '../@services';
import { ChangePasswordService } from './change-password.service';

@Component({
  selector: 'app-change-password',
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    ReactiveFormsModule,
    ContainerComponent,
    RowComponent,
    ColComponent,
    TextColorDirective,
    CardComponent,
    CardBodyComponent,
    FormDirective,
    InputGroupComponent,
    InputGroupTextDirective,
    IconDirective,
    FormControlDirective,
    ButtonDirective,
    FormFeedbackComponent,
    TranslationPipe,
    AlertComponent,
    CaptchaInputComponent,
  ],
  templateUrl: './change-password.component.html',
  styleUrl: './change-password.component.scss',
  providers: [ChangePasswordService],
})
export class ChangePasswordComponent {
  readonly icons = ICON_SUBSET;
  readonly form!: FormGroup;
  readonly ValidatorValues = {
    password: {
      minLength: 8,
      maxLengh: 48,
    },
  };
  readonly mode!: PageMode;

  model: PasswordToken = {} as PasswordToken;
  submitted = false;
  result?: ApiResult;

  /// Adaptive captcha for the anonymous reset endpoint (forgotten mode
  /// only): hidden until the server answers 428.
  captcha_required = false;

  @ViewChild(CaptchaInputComponent)
  private readonly captcha_input?: CaptchaInputComponent;

  get change(): PageMode {
    return PageMode.change;
  }

  get ovpn(): PageMode {
    return PageMode.ovpn;
  }

  get rotate(): PageMode {
    return PageMode.rotate;
  }

  get forgotten(): PageMode {
    return PageMode.forgotten;
  }

  get hasSubUsers(): boolean {
    return this.user_service.hasSubUsers;
  }

  get targetName(): string | undefined {
    return this.user_service.targetName;
  }

  constructor(
    private readonly form_builder: FormBuilder,
    private readonly service: ChangePasswordService,
    private readonly router: Router,
    private readonly route: ActivatedRoute,
    private readonly user_service: UserService
  ) {
    switch (route.routeConfig?.path) {
      case 'change-ovpn-password':
        this.mode = PageMode.ovpn;
        break;
      case 'rotate-ovpn-id':
        this.mode = PageMode.rotate;
        break;
      case 'change-password':
        this.mode = PageMode.change;
        break;
      case 'reset-password':
        this.mode = PageMode.forgotten;
        break;
    }

    if (this.mode === PageMode.forgotten) {
      const token = this.route.snapshot.queryParamMap.get('token') ?? undefined;
      if (token) this.model.token = token;
    }

    this.form = this.createForm();
  }

  submit() {
    if (!this.onValidate()) return;

    if (this.mode === PageMode.forgotten && this.captcha_required && this.captcha_input?.empty) {
      return;
    }

    this.model = { ...this.model, ...this.form.value } as PasswordToken;

    if (this.mode === PageMode.ovpn || this.mode === PageMode.rotate) {
      (this.model as OvpnPasswordToken).target = this.user_service.targetName;
    }

    const headers = this.mode === PageMode.forgotten ? this.captcha_headers() : undefined;

    const job =
      this.mode === PageMode.ovpn || this.mode === PageMode.rotate
        ? this.service.changeOpenVpnPassword(this.model as OvpnPasswordToken)
        : this.mode === PageMode.forgotten
          ? this.service.resetPassword(this.model, headers)
          : this.service.changePassword(this.model);

    job.subscribe((result) => {
      if (result.code === 428) {
        this.result = result;
        this.showCaptcha();
        return;
      }

      // the captcha is single-use: every further attempt needs a fresh image
      if (this.mode === PageMode.forgotten && this.captcha_required) {
        this.captcha_input?.refresh();
      }

      if (result.status() === ResultStatus.success) {
        if (this.mode === PageMode.forgotten)
          setTimeout(() => this.router.navigate(['login']), 1000);
        else setTimeout(() => this.router.navigate(['dashboard']), 1000);
      }

      this.result = result;
    });
  }

  private showCaptcha(): void {
    if (this.captcha_required) {
      this.captcha_input?.refresh();
    } else {
      this.captcha_required = true;
    }
  }

  private captcha_headers(): Record<string, string> | undefined {
    return this.captcha_required ? this.captcha_input?.headers() : undefined;
  }

  private onValidate() {
    this.submitted = true;
    return this.form.status === 'VALID';
  }

  private createForm() {
    // rotation re-authenticates with the account token only: the x-ui
    // data-plane generates the new UUID server-side, no password field applies
    const token_only = this.mode === PageMode.rotate;

    const controls: any = {};

    if (!token_only) {
      controls['password'] = [
        '',
        [
          Validators.required,
          Validators.minLength(this.ValidatorValues.password.minLength),
          Validators.maxLength(this.ValidatorValues.password.maxLengh),
          Validators.pattern(PasswordValidators.ValidationPattern),
        ],
      ];
      controls['confirmPassword'] = [
        '',
        [
          Validators.required,
          Validators.minLength(this.ValidatorValues.password.minLength),
          Validators.maxLength(this.ValidatorValues.password.maxLengh),
        ],
      ];
    }

    if (this.mode !== PageMode.forgotten || !this.model.token) {
      controls['token'] = ['', [Validators.required]];
    }

    const formControl = this.form_builder.group(controls, {
      validators: token_only ? [] : [PasswordValidators.confirmPassword],
    });

    return formControl;
  }
}

enum PageMode {
  change = 'change',
  ovpn = 'ovpn',
  rotate = 'rotate',
  forgotten = 'forgotten',
}
