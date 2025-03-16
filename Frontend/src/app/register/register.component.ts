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
import { ActivatedRoute, RouterLink } from '@angular/router';
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
    TextColorDirective,
  ],
  providers: [RegisterService],
})
export class RegisterComponent {
  readonly icons = ICON_SUBSET;
  readonly form!: FormGroup;
  readonly ValidatorValues = {
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
  readonly edit!: boolean;

  model: NewUserModel = {} as NewUserModel;
  submitted = false;
  result?: ApiResult;
  usernameSuffic: string = '--';

  constructor(
    private readonly form_builder: FormBuilder,
    private readonly service: RegisterService,
    route: ActivatedRoute
  ) {
    this.edit = route.routeConfig?.path === 'edit-user-info';
    this.form = this.createForm();
  }

  ngOnInit(): void {
    if (this.edit) {
      this.service.fullInfo().subscribe((user) => {
        Object.assign(this.model, user);
        const username_parts = this.model?.username?.split('@');
        if (username_parts) {
          if (username_parts.length > 0) {
            this.usernameSuffic = this.model.username.substring(username_parts[0].length + 1);
          }
          this.model.username = username_parts[0];
        }

        const model = this.model as any;
        for (const key in this.model) {
          this.form.controls[key]?.setValue(model[key]);
        }
      });
    }
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
            Validators.minLength(this.ValidatorValues.username.minLength),
            Validators.maxLength(this.ValidatorValues.username.maxLengh),
            Validators.pattern('^[a-zA-Z][_\\-\\.a-zA-Z0-9]+$'),
          ],
        ],
        email: ['', [Validators.required, Validators.email]],
        mobile: [
          '',
          [
            Validators.minLength(this.ValidatorValues.mobile.minLength),
            Validators.maxLength(this.ValidatorValues.mobile.maxLengh),
            Validators.pattern('^\\+?\\d+$'),
          ],
        ],
        firstname: ['', []],
        lastname: ['', []],
        password: [
          '',
          [
            Validators.required,
            Validators.minLength(this.ValidatorValues.password.minLength),
            Validators.maxLength(this.ValidatorValues.password.maxLengh),
            Validators.pattern(
              '((?=.*\\d)(?=.*[a-z])(?=.*[A-Z])|(?=.*[^\\x00-\\x7F])).+'
            ),
          ],
        ],
        confirmPassword: [
          '',
          [
            Validators.required,
            Validators.minLength(this.ValidatorValues.password.minLength),
            Validators.maxLength(this.ValidatorValues.password.maxLengh),
          ],
        ],
      },
      { validators: [PasswordValidators.confirmPassword] }
    );

    return formControl;
  }
}

class PasswordValidators {
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
