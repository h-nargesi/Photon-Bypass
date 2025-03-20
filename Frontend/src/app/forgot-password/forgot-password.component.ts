import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
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

    const emailMobile = this.form.controls['emailMobile'].value;

    this.service.reset(emailMobile).subscribe((result) => {
      this.result = result;

      if (result.status() === ResultStatus.success) {
        setTimeout(() => this.router.navigate(['login']), 1000);
      }
    });
  }

  getColor(code: ResultStatus) {
    return MessageService.getStatusCode(code);
  }

  private onValidate() {
    this.submitted = true;
    return this.form.status === 'VALID';
  }

  private createForm() {
    const formControl = this.form_builder.group({
      emailMobile: ['', [Validators.required, Validators.email]],
    });

    return formControl;
  }
}
