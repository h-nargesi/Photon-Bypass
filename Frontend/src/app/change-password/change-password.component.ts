import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import {
  FormBuilder,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
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

  get change(): PageMode {
    return PageMode.change;
  }

  get ovpn(): PageMode {
    return PageMode.ovpn;
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
      else this.router.navigate(['']);
    }

    this.form = this.createForm();
  }

  submit() {
    if (!this.onValidate()) return;

    if (this.mode === PageMode.ovpn) {
      (this.model as OvpnPasswordToken).target = this.user_service.targetName;
    }

    const job =
      this.mode === PageMode.ovpn
        ? this.service.changeOpenVpnPassword(this.model as OvpnPasswordToken)
        : this.service.changePassword(this.model);

    job.subscribe((result) => {
      if (result.status() === ResultStatus.success) {
        if (this.mode === PageMode.forgotten)
          setTimeout(() => this.router.navigate(['login']), 1000);
        else setTimeout(() => this.router.navigate(['dashboard']), 1000);
      }

      this.result = result;
    });
  }

  private onValidate() {
    this.submitted = true;
    return this.form.status === 'VALID';
  }

  private createForm() {
    const controls: any = {
      password: [
        '',
        [
          Validators.required,
          Validators.minLength(this.ValidatorValues.password.minLength),
          Validators.maxLength(this.ValidatorValues.password.maxLengh),
          Validators.pattern(PasswordValidators.ValidationPattern),
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
    };

    if (this.mode !== PageMode.forgotten) {
      controls['token'] = ['', [Validators.required]];
    }

    const formControl = this.form_builder.group(controls, {
      validators: [PasswordValidators.confirmPassword],
    });

    return formControl;
  }
}

enum PageMode {
  change = 'change',
  ovpn = 'ovpn',
  forgotten = 'forgotten',
}
