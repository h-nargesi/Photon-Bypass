import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
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
import { ApiResult, NewUserModel } from '../@models';
import { TranslationPipe } from '../@services';
import { RegisterService } from './register.service';

@Component({
  selector: 'app-register',
  templateUrl: './register.component.html',
  styleUrls: ['./register.component.scss'],
  imports: [
    CommonModule,
    FormsModule,
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
    TranslationPipe,
    RouterLink,
    FormFeedbackComponent,
  ],
  providers: [RegisterService],
})
export class RegisterComponent {
  readonly icons = ICON_SUBSET;
  readonly form!: FormGroup;
  readonly validatorValues = {
    username: {
      minLength: 5,
      maxLengh: 32,
    },
    mobile: {
      minLength: 5,
      maxLengh: 16,
    },
    password: {
      minLength: 8,
      maxLengh: 48,
    },
  };

  model: NewUserModel = {} as NewUserModel;
  submitted = false;
  result?: ApiResult;

  constructor(
    private readonly form_builder: FormBuilder,
    private readonly service: RegisterService
  ) {
    this.form = this.createForm();
  }

  onValidate() {
    this.submitted = true;
    if (this.form.status !== 'VALID') console.warn(this.form);
    return this.form.status === 'VALID';
  }

  submit() {
    if (!this.onValidate()) return;

    this.service.register(this.model).subscribe((result) => {
      if (result.code === 200) {
        this.model = {} as NewUserModel;
      }

      this.result = result;
    });
  }

  private createForm() {
    const formControl = this.form_builder.group(
      {
        username: [
          '',
          [
            Validators.required,
            Validators.minLength(this.validatorValues.username.minLength),
            Validators.maxLength(this.validatorValues.username.maxLengh),
            Validators.pattern('^[a-zA-Z][_\\-\\.a-zA-Z0-9]+$'),
          ],
        ],
        email: ['', [Validators.required, Validators.email]],
        mobile: [
          '',
          [
            Validators.minLength(this.validatorValues.mobile.minLength),
            Validators.maxLength(this.validatorValues.mobile.maxLengh),
            Validators.pattern('^\\+?\\d+$'),
          ],
        ],
        firstname: ['', []],
        lastname: ['', []],
        password: [
          '',
          [
            Validators.required,
            Validators.minLength(this.validatorValues.password.minLength),
            Validators.maxLength(this.validatorValues.password.maxLengh),
            Validators.pattern(
              '((?=.*\\d)(?=.*[a-z])(?=.*[A-Z])|(?=.*[^\\x00-\\x7F])).+'
            ),
          ],
        ],
        confirmPassword: [
          '',
          [
            Validators.required,
            Validators.minLength(this.validatorValues.password.minLength),
            Validators.maxLength(this.validatorValues.password.maxLengh),
          ],
        ],
      },
      { validators: [PasswordValidators.confirmPassword] }
    );

    return formControl;
  }
}

export class PasswordValidators {
  static confirmPassword(control: AbstractControl): ValidationErrors | null {
    const password = control.get('password');
    const confirm = control.get('confirmPassword');
    if (password?.valid && password?.value === confirm?.value) {
      confirm?.setErrors(null);
      return null;
    }
    confirm?.setErrors({ passwordMismatch: true });
    return { passwordMismatch: true };
  }
}
