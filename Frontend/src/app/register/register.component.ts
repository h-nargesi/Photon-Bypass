import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
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
  SpinnerComponent,
  TextColorDirective,
  TooltipModule,
} from '@coreui/angular';
import { IconDirective } from '@coreui/icons-angular';
import { ICON_SUBSET } from '../@icons';
import { ApiResult, RegisterModel, ResultStatus } from '../@models';
import {
  AppConfigService,
  MessageService,
  PasswordValidators,
  TranslationPipe,
  TranslationService,
  UserService,
} from '../@services';
import { RegisterService } from './register.service';

@Component({
  selector: 'app-register',
  templateUrl: './register.component.html',
  styleUrls: ['./register.component.scss'],
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
    TextColorDirective,
    FormDirective,
    InputGroupComponent,
    InputGroupTextDirective,
    IconDirective,
    FormControlDirective,
    ButtonDirective,
    FormFeedbackComponent,
    SpinnerComponent,
    AlertComponent,
    TooltipModule,
    TranslationPipe,
  ],
  providers: [RegisterService],
})
export class RegisterComponent implements OnInit {
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
  readonly mode!: PageMode;

  target?: string;
  model: RegisterModel = {} as RegisterModel;
  submitted = false;
  result?: ApiResult;
  usernameSuffic: string = ' - -';

  /// Telegram connection card (edit mode only, D2): status loads with the
  /// form, "connect" issues a one-time deep-link code, "disconnect" detaches.
  telegram_connected = false;
  telegram_link?: { code: string; url: string };
  telegram_busy = false;

  /// D11: public registration can be disabled via app-config — the form
  /// is replaced by a closed notice (server enforces the same flag anyway).
  allow_registration = false;

  constructor(
    private readonly form_builder: FormBuilder,
    private readonly service: RegisterService,
    private readonly app_config: AppConfigService,
    private readonly user_service: UserService,
    private readonly messages: MessageService,
    private readonly translation: TranslationService,
    private readonly router: Router,
    route: ActivatedRoute
  ) {
    switch (route.routeConfig?.path) {
      case 'edit-account-info':
        this.mode = PageMode.EditAccount;
        break;
      case 'edit-user-info':
        this.mode = PageMode.EditSub;
        break;
      default:
        this.mode = PageMode.Register;
        break;
    }
    this.form = this.createForm();
  }

  private readonly destroy_ref = inject(DestroyRef);

  get isEdit(): boolean {
    return this.mode !== PageMode.Register;
  }

  ngOnInit(): void {
    if (this.mode === PageMode.Register) {
      this.app_config.config().subscribe((config) => {
        this.allow_registration = config?.allowRegistration === true;
      });
      return;
    }
    this.loadUserFullData();
    this.loadTelegramStatus();

    if (this.mode !== PageMode.EditSub) return;
    this.user_service.onTargetChanged
      .pipe(takeUntilDestroyed(this.destroy_ref))
      .subscribe(() => {
        this.loadUserFullData();
        this.loadTelegramStatus();
      });
  }

  submit() {
    if (!this.onValidate()) return;

    this.model = { ...this.form.value } as RegisterModel;

    const job =
      this.mode !== PageMode.Register
        ? this.service.edit(this.model, this.target)
        : this.service.register(this.model);

    job.subscribe((result) => {
      this.result = result;
      this.submitted = false;
      if (result.status() !== ResultStatus.success) return;

      this.user_service.reload();

      if (this.mode === PageMode.Register) {
        setTimeout(() => this.router.navigate(['login']), 1000);
      }
    });
  }

  getColor(code: ResultStatus) {
    return MessageService.getStatusCode(code);
  }

  createTelegramLink() {
    this.telegram_busy = true;

    this.service.createTelegramLink(this.target).subscribe({
      next: (state) => {
        this.telegram_busy = false;

        if (state?.code && state.url) {
          this.telegram_link = { code: state.code, url: state.url };
          this.telegram_connected = state.connected;
        }
      },
      error: () => (this.telegram_busy = false),
    });
  }

  disconnectTelegram() {
    this.telegram_busy = true;

    this.service.disconnectTelegram(this.target).subscribe({
      next: () => {
        this.telegram_busy = false;
        this.telegram_connected = false;
        this.telegram_link = undefined;
      },
      error: () => (this.telegram_busy = false),
    });
  }

  copyTelegramCode(code: string) {
    if (!navigator.clipboard) return;

    navigator.clipboard.writeText(code).then(() => {
      this.messages.messageHandler(
        ResultStatus.success,
        this.translation.translate('register.telegram.title'),
        this.translation.translate('register.telegram.code-copied'),
      );
    });
  }

  private loadTelegramStatus() {
    this.telegram_busy = true;

    this.service.telegramLinkStatus(this.target).subscribe({
      next: (state) => {
        this.telegram_connected = state?.connected === true;
        this.telegram_busy = false;
      },
      error: () => (this.telegram_busy = false),
    });
  }

  private async loadUserFullData() {
    this.submitted = true;

    if (this.mode === PageMode.EditSub)
      this.target =
        this.user_service.targetName ??
        (await this.user_service.user()).username;

    this.service.fullInfo(this.target).subscribe((user) => {
      Object.assign(this.model, user);
      const username_parts = this.model?.username?.split('@');
      if (username_parts) {
        if (username_parts.length > 0) {
          this.usernameSuffic = this.model.username.substring(
            username_parts[0].length + 1
          );
        }
        this.model.username = username_parts[0];
      }

      // const model = this.model as any;
      // for (const key in this.model) {
      //   this.form.controls[key]?.setValue(model[key]);
      // }

      this.form.patchValue(this.model);
      this.submitted = false;
    });
  }

  private onValidate() {
    this.submitted = true;
    return this.form.status === 'VALID';
  }

  private createForm() {
    const controllers: any = {
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
    };

    let validators: any = undefined;

    if (this.mode === PageMode.Register) {
      controllers['password'] = [
        '',
        [
          Validators.required,
          Validators.minLength(this.ValidatorValues.password.minLength),
          Validators.maxLength(this.ValidatorValues.password.maxLengh),
          Validators.pattern(PasswordValidators.ValidationPattern),
        ],
      ];
      controllers['confirmPassword'] = [
        '',
        [
          Validators.required,
          Validators.minLength(this.ValidatorValues.password.minLength),
          Validators.maxLength(this.ValidatorValues.password.maxLengh),
        ],
      ];
      validators = [PasswordValidators.confirmPassword];
    }

    const formControl = this.form_builder.group(controllers, {
      validators,
    });

    return formControl;
  }
}

enum PageMode {
  EditAccount,
  EditSub,
  Register,
}
