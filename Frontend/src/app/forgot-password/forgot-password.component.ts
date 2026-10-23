import { CommonModule } from '@angular/common';
import { Component, ViewChild } from '@angular/core';
import {
  FormBuilder,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
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
import { ApiResult, ResultStatus } from '../@models';
import { MessageService, TranslationPipe } from '../@services';
import { ForgotPasswordService } from './forgot-password.service';

@Component({
  selector: 'app-forget-password',
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterLink,
    ContainerComponent,
    RowComponent,
    ColComponent,
    CardComponent,
    CardBodyComponent,
    FormDirective,
    InputGroupComponent,
    InputGroupTextDirective,
    IconDirective,
    ButtonDirective,
    FormFeedbackComponent,
    AlertComponent,
    TextColorDirective,
    FormControlDirective,
    TranslationPipe,
    CaptchaInputComponent,
  ],
  templateUrl: './forgot-password.component.html',
  styleUrl: './forgot-password.component.scss',
  providers: [ForgotPasswordService],
})
export class ForgotPasswordComponent {
  readonly icons = ICON_SUBSET;
  readonly form!: FormGroup;

  isValidated = false;
  userInfo?: string;
  submitted = false;
  result?: ApiResult;

  /// Adaptive captcha: hidden until the server answers 428.
  captcha_required = false;

  @ViewChild(CaptchaInputComponent)
  private readonly captcha_input?: CaptchaInputComponent;

  constructor(
    private readonly form_builder: FormBuilder,
    private readonly service: ForgotPasswordService,
    private readonly router: Router
  ) {
    this.form = this.createForm();
  }

  getEmailMobile() {
    return this.form.controls['emailMobile'];
  }

  submit() {
    if (!this.onValidate()) return;

    if (this.captcha_required && this.captcha_input?.empty) return;

    const emailMobile = this.form.controls['emailMobile'].value;

    this.service.reset(emailMobile, this.captcha_headers()).subscribe((result) => {
      this.result = result;

      if (result.code === 428) {
        this.showCaptcha();
        return;
      }

      // the captcha is single-use: every further attempt needs a fresh image
      if (this.captcha_required) this.captcha_input?.refresh();

      if (result.status() === ResultStatus.success) {
        setTimeout(() => this.router.navigate(['login']), 1000);
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
    // D4: the input is an email OR a username — the server routes the code
    // (telegram chat → social message, valid email → mail, else neutral),
    // so only presence is enforced client-side
    const formControl = this.form_builder.group({
      emailMobile: ['', [Validators.required]],
    });

    return formControl;
  }
}
