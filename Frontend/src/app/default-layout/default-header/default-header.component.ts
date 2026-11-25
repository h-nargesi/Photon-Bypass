import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectorRef,
  Component,
  computed,
  DestroyRef,
  inject,
  Input,
  OnInit,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Title } from '@angular/platform-browser';
import { merge } from 'rxjs';

import {
  AlertComponent,
  ButtonDirective,
  ColorModeService,
  ContainerComponent,
  DropdownComponent,
  DropdownItemDirective,
  DropdownMenuDirective,
  DropdownToggleDirective,
  HeaderComponent,
  HeaderNavComponent,
  ModalService,
  ModalToggleDirective,
  NavLinkDirective,
  TextColorDirective,
  TooltipDirective,
} from '@coreui/angular';

import { Router, RouterLink } from '@angular/router';
import { IconDirective } from '@coreui/icons-angular';
import { ICON_SUBSET } from '../../@icons';
import { UserModel, UserVpnAccount } from '../../@models';
import { printMoney, TranslationPipe, UserService } from '../../@services';

@Component({
  selector: 'app-default-header',
  templateUrl: './default-header.component.html',
  imports: [
    NgTemplateOutlet,
    NavLinkDirective,
    RouterLink,
    ContainerComponent,
    IconDirective,
    HeaderNavComponent,
    DropdownComponent,
    DropdownToggleDirective,
    DropdownMenuDirective,
    DropdownItemDirective,
    ButtonDirective,
    TextColorDirective,
    AlertComponent,
    ModalToggleDirective,
    TooltipDirective,
    TranslationPipe,
  ],
})
export class DefaultHeaderComponent extends HeaderComponent implements OnInit {
  readonly icons = ICON_SUBSET;

  readonly #colorModeService = inject(ColorModeService);
  readonly colorMode = this.#colorModeService.colorMode;

  readonly colorModes = [
    { name: 'light', text: 'روشن', icon: this.icons.cilSun },
    { name: 'dark', text: 'تیره', icon: this.icons.cilMoon },
    { name: 'auto', text: 'خودکار', icon: this.icons.cilContrast },
  ];

  current_user?: UserModel;

  /// Copy of the active user's VPN accounts. Refreshed when the target or
  /// the selected account changes, so the menu cannot keep a stale list.
  accountRows: UserVpnAccount[] = [];

  private readonly change_detector = inject(ChangeDetectorRef);
  private readonly destroy_ref = inject(DestroyRef);

  @Input() userSelector?: string;
  @Input() paymentModal?: string;

  constructor(
    public readonly title_service: Title,
    private readonly user_service: UserService,
    private readonly modal_service: ModalService,
    private readonly router: Router
  ) {
    super();
  }

  readonly icon_cilSun = computed(() => {
    const currentMode = this.colorMode();
    return (
      this.colorModes.find((mode) => mode.name === currentMode)?.icon ??
      this.icons.cilSun
    );
  });

  async ngOnInit() {
    this.current_user = await this.user_service.user();
    this.refreshAccounts();

    merge(this.user_service.onTargetChanged, this.user_service.onAccountChanged)
      .pipe(takeUntilDestroyed(this.destroy_ref))
      .subscribe(() => {
        this.refreshAccounts();
        this.change_detector.detectChanges();
      });
  }

  get hasSubUsers(): boolean {
    return this.user_service.hasSubUsers;
  }

  /// target selector: managers pick from their targetArea, the admin picks
  /// from the full user list (Docs/20 phase 6 step 22)
  get canSelectTarget(): boolean {
    return this.hasSubUsers || this.user_service.isAdmin;
  }

  /// D6: dependent accounts see no financial UI at all
  get isSelfService(): boolean {
    return this.user_service.isSelfService;
  }

  get isManager(): boolean {
    return this.user_service.isManager;
  }

  get isAdmin(): boolean {
    return this.user_service.isAdmin;
  }

  get targetName(): string | undefined {
    return this.user_service.targetName;
  }

  /// The account menu lists every VPN account of the selected user. For the
  /// acting user it stays hidden until there is more than one.
  get showAccountMenu(): boolean {
    return this.accountRows.length > 0
      && (this.user_service.viewingOtherUser || this.accountRows.length > 1);
  }

  /// Wallet, payment and management links belong to the logged-in user.
  /// They sit behind a divider when that group has anything to show.
  get showsActorLinks(): boolean {
    return this.isSelfService || this.isManager;
  }

  get currentAccountId(): number | undefined {
    return this.user_service.accountId;
  }

  get currentAccountUsername(): string {
    return this.user_service.currentAccount?.username ?? '';
  }

  switchAccount(accountId: number) {
    this.user_service.setAccount(accountId);
  }

  private refreshAccounts() {
    this.accountRows = this.user_service.accounts;
  }

  goPayment() {
    if (this.user_service.invoice) {
      this.router.navigate(['payment'], {
        queryParams: { invoice: this.user_service.invoice },
      });
    } else {
      this.modal_service.toggle({
        show: true,
        id: this.paymentModal,
      });
    }
  }

  showBalance(): string {
    return printMoney(this.current_user?.balance);
  }
}
