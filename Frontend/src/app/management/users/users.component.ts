import { CommonModule } from '@angular/common';
import { Component, OnInit, ViewChild } from '@angular/core';
import {
  FormBuilder,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import {
  MatPaginator,
  MatPaginatorIntl,
  MatPaginatorModule,
  PageEvent,
} from '@angular/material/paginator';
import { MatTableModule } from '@angular/material/table';
import { Router } from '@angular/router';
import {
  BadgeComponent,
  ButtonCloseDirective,
  ButtonDirective,
  CardBodyComponent,
  CardComponent,
  ColComponent,
  FormControlDirective,
  FormCheckInputDirective,
  FormCheckLabelDirective,
  FormDirective,
  FormFeedbackComponent,
  InputGroupComponent,
  InputGroupTextDirective,
  ModalBodyComponent,
  ModalComponent,
  ModalFooterComponent,
  ModalHeaderComponent,
  ModalTitleDirective,
  RowComponent,
  TooltipDirective,
} from '@coreui/angular';
import { IconDirective } from '@coreui/icons-angular';
import { ICON_SUBSET } from '../../@icons';
import {
  AccountRole,
  AccountStatus,
  CreateUserModel,
  ResultStatus,
  UserRow,
} from '../../@models';
import {
  MessageService,
  PasswordValidators,
  TranslationPipe,
  TranslationService,
  UserService,
} from '../../@services';
import { PaginatorInt } from '../../history/paginator-int';
import { UsersService } from './users.service';

@Component({
  selector: 'app-users',
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RowComponent,
    ColComponent,
    CardComponent,
    CardBodyComponent,
    InputGroupComponent,
    InputGroupTextDirective,
    IconDirective,
    FormControlDirective,
    ButtonDirective,
    BadgeComponent,
    TranslationPipe,
    TooltipDirective,
    FormDirective,
    FormCheckInputDirective,
    FormCheckLabelDirective,
    FormFeedbackComponent,
    ModalComponent,
    ModalHeaderComponent,
    ModalTitleDirective,
    ModalBodyComponent,
    ModalFooterComponent,
    ButtonCloseDirective,
    MatFormFieldModule,
    MatInputModule,
    MatTableModule,
    MatPaginatorModule,
  ],
  templateUrl: './users.component.html',
  styleUrl: './users.component.scss',
  providers: [{ provide: MatPaginatorIntl, useClass: PaginatorInt }],
})
export class UsersComponent implements OnInit {
  readonly icons = ICON_SUBSET;
  readonly page_size = 20;
  readonly roles = [AccountRole.User, AccountRole.Manager];

  rows: UserRow[] = [];
  total = 0;
  page = 1;
  search = '';
  loading = false;

  readonly create_form: FormGroup;
  create_visible = false;
  create_submitted = false;
  create_allow_self_service = true;
  create_role: AccountRole = AccountRole.User;
  create_owner = '';

  password_visible = false;
  role_visible = false;
  owner_visible = false;
  busy = false;

  selected?: UserRow;
  new_password?: string;
  new_role: AccountRole = AccountRole.User;
  new_owner?: string;

  @ViewChild(MatPaginator) paginator?: MatPaginator;

  constructor(
    private readonly service: UsersService,
    private readonly user_service: UserService,
    private readonly router: Router,
    private readonly messages: MessageService,
    private readonly translation: TranslationService,
    form_builder: FormBuilder,
  ) {
    this.create_form = this.buildCreateForm(form_builder);
  }

  get isAdmin(): boolean {
    return this.user_service.isAdmin;
  }

  get displayedColumns(): string[] {
    const columns = ['username', 'fullname', 'email'];
    if (this.isAdmin) columns.push('role');
    columns.push('type', 'status');
    if (this.isAdmin) columns.push('owner');
    columns.push('plan', 'created', 'actions');
    return columns;
  }

  ngOnInit(): void {
    this.load();
  }

  load(page: number = this.page): void {
    this.loading = true;

    this.service.list(this.search.trim() || undefined, page).subscribe({
      next: (result) => {
        this.loading = false;
        this.rows = result?.items ?? [];
        this.total = result?.totalCount ?? 0;
        this.page = result?.page ?? page;
      },
      error: () => (this.loading = false),
    });
  }

  onPage(event: PageEvent): void {
    this.load(event.pageIndex + 1);
  }

  applySearch(): void {
    this.paginator?.firstPage();
    this.load(1);
  }

  fullname(row: UserRow): string {
    return [row.name, row.surname].filter(Boolean).join(' ');
  }

  roleTitle(role: AccountRole): string {
    return this.translation.translate(
      'management.users.roles.' + AccountRole[role].toLowerCase(),
    );
  }

  statusTitle(status: AccountStatus): string {
    return this.translation.translate(
      'management.users.status.' +
        (status === AccountStatus.Active
          ? 'active'
          : status === AccountStatus.VpnSuspended
            ? 'vpn-suspended'
            : 'inactive'),
    );
  }

  statusColor(status: AccountStatus): string {
    return status === AccountStatus.Active
      ? 'success'
      : status === AccountStatus.VpnSuspended
        ? 'warning'
        : 'danger';
  }

  expirationTitle(row: UserRow): string {
    return row.expirationDate
      ? new Date(row.expirationDate).toLocaleDateString('fa-IR')
      : '';
  }

  // --- create (docs/20 phase 6 step 21; admin extras per phase 4) ---

  openCreate(): void {
    this.create_form.reset();
    this.create_submitted = false;
    this.create_allow_self_service = true;
    this.create_role = AccountRole.User;
    this.create_owner = '';
    this.create_visible = true;
  }

  submitCreate(): void {
    this.create_submitted = true;
    if (this.create_form.invalid || this.busy) return;

    const value = this.create_form.value;

    const model: CreateUserModel = {
      username: value.username,
      password: value.password,
      email: value.email,
      mobile: value.mobile || undefined,
      firstname: value.firstname || undefined,
      lastname: value.lastname || undefined,
      allowSelfService: this.create_allow_self_service,
    };

    if (this.isAdmin) {
      model.role = this.create_role;
      model.owner = this.create_owner.trim() || undefined;
    }

    this.busy = true;
    this.service.create(model).subscribe((result) => {
      this.busy = false;
      if (result.status() !== ResultStatus.success) return;

      this.create_visible = false;
      // refresh the cached target-area so the new subordinate is reachable
      // for on-behalf actions in this session (cache fills on next user())
      this.user_service.reload();
      this.applySearch();
    });
  }

  isInvalid(name: string): boolean {
    const control = this.create_form.get(name);
    return !!control && control.invalid && (this.create_submitted || control.touched);
  }

  // --- password reset (D12/D13) ---

  openPassword(row: UserRow): void {
    this.selected = row;
    this.new_password = undefined;
    this.password_visible = true;
  }

  submitPassword(): void {
    const password = this.new_password ?? '';
    if (!this.selected || this.busy) return;

    if (
      password.length < 8 ||
      password.length > 48 ||
      !new RegExp(PasswordValidators.ValidationPattern).test(password)
    ) {
      this.messages.messageHandler(
        ResultStatus.error,
        this.translation.translate('management.users.modals.password-title'),
        this.translation.translate('register.messages.password-requirement'),
      );
      return;
    }

    this.busy = true;
    this.service.setPassword(this.selected.username, password).subscribe((result) => {
      this.busy = false;
      if (result.status() !== ResultStatus.success) return;

      this.password_visible = false;
      this.new_password = undefined;
    });
  }

  // --- role (admin only, User↔Manager — D9) ---

  openRole(row: UserRow): void {
    this.selected = row;
    this.new_role = row.role === AccountRole.Admin ? AccountRole.User : row.role;
    this.role_visible = true;
  }

  submitRole(): void {
    if (!this.selected || this.busy) return;

    this.busy = true;
    this.service.setRole(this.selected.username, this.new_role).subscribe((result) => {
      this.busy = false;
      if (result.status() !== ResultStatus.success) return;

      this.role_visible = false;
      this.load();
    });
  }

  // --- owner attach/detach (admin only) ---

  openOwner(row: UserRow): void {
    this.selected = row;
    this.new_owner = row.owner ?? '';
    this.owner_visible = true;
  }

  submitOwner(): void {
    if (!this.selected || this.busy) return;

    this.busy = true;
    this.service
      .setOwner(this.selected.username, this.new_owner?.trim() || null)
      .subscribe((result) => {
        this.busy = false;
        if (result.status() !== ResultStatus.success) return;

        this.owner_visible = false;
        this.load();
      });
  }

  // --- toggles (D13 + set-self-service matrix) ---

  toggleSelfService(row: UserRow): void {
    if (this.busy) return;

    this.busy = true;
    this.service.setSelfService(row.username, !row.allowSelfService).subscribe((result) => {
      this.busy = false;
      if (result.status() !== ResultStatus.success) return;

      this.load();
    });
  }

  /// VPN suspension (0↔1): portal login stays, only the VPN config goes off;
  /// a fully deactivated account has no VPN to toggle (D13).
  toggleVpn(row: UserRow): void {
    if (this.busy || row.status === AccountStatus.Deactivated) return;

    this.setStatus(row, row.status === AccountStatus.VpnSuspended
      ? AccountStatus.Active
      : AccountStatus.VpnSuspended);
  }

  /// Full deactivation (→2) and reactivation (→0): at VpnSuspended the same
  /// button escalates to a full deactivation.
  toggleActive(row: UserRow): void {
    if (this.busy) return;

    this.setStatus(row, row.status === AccountStatus.Deactivated
      ? AccountStatus.Active
      : AccountStatus.Deactivated);
  }

  private setStatus(row: UserRow, status: AccountStatus): void {
    this.busy = true;
    this.service.setStatus(row.username, status).subscribe((result) => {
      this.busy = false;
      if (result.status() !== ResultStatus.success) return;

      this.load();
    });
  }

  // --- act on behalf (docs/20 phase 6 step 21/22) ---

  actOnBehalf(row: UserRow): void {
    if (row.status === AccountStatus.Deactivated) return;

    if (this.isAdmin) {
      this.user_service.setExternalTarget({
        username: row.username,
        fullname: this.fullname(row),
        email: row.email ?? '',
      });
    } else {
      this.user_service.setTraget(row.username);
    }

    this.router.navigate(['/dashboard']);
  }

  private buildCreateForm(form_builder: FormBuilder): FormGroup {
    return form_builder.group({
      username: [
        '',
        [
          Validators.required,
          Validators.minLength(5),
          Validators.maxLength(32),
          Validators.pattern('^[a-zA-Z][_\\-\\.a-zA-Z0-9]+$'),
        ],
      ],
      password: [
        '',
        [
          Validators.required,
          Validators.minLength(8),
          Validators.maxLength(48),
          Validators.pattern(PasswordValidators.ValidationPattern),
        ],
      ],
      email: ['', [Validators.required, Validators.email]],
      mobile: ['', [Validators.pattern('^\\+?\\d+$')]],
      firstname: [''],
      lastname: [''],
    });
  }
}
