import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject, catchError, EMPTY, switchMap } from 'rxjs';
import {
  BorderDirective,
  ButtonDirective,
  CardBodyComponent,
  CardComponent,
  CardFooterComponent,
  CardHeaderComponent,
  ColComponent,
  FormSelectDirective,
  PlaceholderAnimationDirective,
  PlaceholderDirective,
  RowComponent,
} from '@coreui/angular';
import {
  NewAccountContext,
  RenewalContext,
  PriceModel,
  RealmChoice,
  RenewalResult,
  UserModel,
} from '../@models';
import {
  printMoney,
  TranslationPipe,
  TranslationService,
  UserService,
} from '../@services';
import { RenewalService } from './renewal.service';

@Component({
  selector: 'app-renewal',
  imports: [
    CommonModule,
    FormsModule,
    RowComponent,
    ColComponent,
    CardComponent,
    CardHeaderComponent,
    CardBodyComponent,
    CardFooterComponent,
    ButtonDirective,
    BorderDirective,
    FormSelectDirective,
    PlaceholderDirective,
    PlaceholderAnimationDirective,
    TranslationPipe,
  ],
  templateUrl: './renewal.component.html',
  styleUrl: './renewal.component.scss',
  providers: [RenewalService],
})
export class RenewalComponent implements OnInit {
  readonly maxUserCounts = [1, 2, 3, 4, 5, 6];
  readonly timeChoises = [30, 60, 90, 120, 150, 180];
  readonly trafficChoises = [25, 50, 75, 100, 150, 200];

  readonly timeUnit!: string;
  readonly trafficUnit!: string;

  color: string = 'secondary';
  selectedTime = 0;
  selectedTraffic = 0;
  selectedUserCount = 0;
  cost: string = '--';

  renewal = {} as RenewalContext;
  current_user!: UserModel;
  prices?: PriceModel[];
  result?: RenewalResult;
  submitting = false;

  /// docs/22 §3.3: the two explicit purchase paths — renewal of the selected
  /// account, or a brand-new account (realm + username).
  mode: 'renew' | 'new-account' = 'renew';
  realms: RealmChoice[] = [];
  newAccount = {} as NewAccountContext;
  requiresRealmChoice = false;

  private readonly estimate$ = new Subject<RenewalContext>();

  constructor(
    private readonly service: RenewalService,
    private readonly user_service: UserService,
    private readonly router: Router,
    translation: TranslationService
  ) {
    this.timeUnit = translation.translate('renewal.labels.monthly.unit');
    this.trafficUnit = translation.translate('renewal.labels.traffic.unit');
  }

  get hasSubUsers(): boolean {
    return this.user_service.hasSubUsers;
  }

  get hasAccounts(): boolean {
    return this.user_service.accounts.length > 0;
  }

  get currentAccountUsername(): string {
    return this.user_service.currentAccount?.username ?? '';
  }

  get targetName(): string | undefined {
    return this.user_service.targetName;
  }

  async ngOnInit() {
    // the account list arrives with the user load — decide the mode after it
    await this.user_service.user();

    if (this.hasAccounts) {
      this.mode = 'renew';
      this.loadLastPlan();
    } else {
      this.mode = 'new-account';
      this.loadNewAccountDefaults();
    }

    this.service.realms().subscribe((realms) => (this.realms = realms));
    this.loadPrcies();
    this.user_service.onAccountChanged.subscribe(() => {
      if (this.mode === 'renew') {
        this.loadLastPlan();
      }
    });

    this.estimate$
      .pipe(
        switchMap((plan) =>
          this.service.estimate(plan).pipe(catchError(() => EMPTY)),
        ),
      )
      .subscribe((cost) => {
        this.color = cost ? 'primary' : 'secondary';
        this.selectedTime = cost.days;
        this.cost = printMoney(cost.price);
      });
  }

  switchMode(mode: 'renew' | 'new-account') {
    this.mode = mode;

    if (mode === 'new-account') {
      this.loadNewAccountDefaults();
    } else {
      this.loadLastPlan();
    }
  }

  private loadNewAccountDefaults() {
    this.service.suggestUsername().subscribe((username) => {
      this.newAccount.username = username;
    });
  }

  selectRealm(realmId: number) {
    if (this.mode === 'new-account') {
      this.newAccount.realmId = realmId;
    } else {
      this.renewal.realmId = realmId;
    }
  }

  submit() {
    if (
      this.submitting ||
      (!this.renewal.gigabytes && !this.renewal.days) ||
      !this.renewal.simultaneousUserCount
    ) {
      return;
    }

    this.submitting = true;

    if (this.mode === 'new-account') {
      this.submitNewAccount();
      return;
    }

    this.renewal.accountId = this.user_service.accountId;
    this.renewal.target = this.user_service.targetName;

    this.service.renewal(this.renewal).subscribe({
      next: async (result) => {
        this.result = result;

        const current_user = await this.user_service.user();
        current_user.balance = result.currentPrice;
        this.user_service.reload();

        // The invoice is always issued on the paying account (docs/20 D4), so
        // the payment/receipt page must open in the payer's own view — the
        // renewed target never owns the invoice.
        if (result.invoiceCode) {
          this.router.navigate(['payment'], {
            queryParams: { invoice: result.invoiceCode },
          });
        }

        this.submitting = false;
      },
      error: () => (this.submitting = false),
    });
  }

  /// docs/22 D9: the first purchase carries realm + VPN username; settlement
  /// (payment modal) creates the account and its first renewal atomically.
  private submitNewAccount() {
    this.newAccount.days = this.renewal.days;
    this.newAccount.gigabytes = this.renewal.gigabytes;
    this.newAccount.simultaneousUserCount = this.renewal.simultaneousUserCount;

    if (!this.newAccount.realmId) {
      this.submitting = false;
      return;
    }

    this.service.newAccount(this.newAccount).subscribe({
      next: async (result) => {
        this.result = result;

        const current_user = await this.user_service.user();
        current_user.balance = result.currentPrice;
        this.user_service.reload();

        if (result.invoiceCode) {
          this.router.navigate(['payment'], {
            queryParams: { invoice: result.invoiceCode },
          });
        }

        this.submitting = false;
      },
      error: () => (this.submitting = false),
    });
  }

  setTime() {
    if (this.selectedTime === 0) return;
    this.color = 'primary';
    if (this.renewal.days === this.selectedTime) return;
    this.renewal.days = this.selectedTime;
    this.fetchEstimate();
  }

  setTraffic() {
    if (this.selectedTraffic === 0) return;
    this.color = 'primary';
    if (this.renewal.gigabytes === this.selectedTraffic) return;
    this.renewal.gigabytes = this.selectedTraffic;
    this.fetchEstimate();
  }

  setUserCount() {
    if (this.renewal.simultaneousUserCount === this.selectedUserCount) return;
    this.renewal.simultaneousUserCount = this.selectedUserCount;
    this.fetchEstimate();
  }

  private fetchEstimate() {
    if (
      (!this.renewal.gigabytes && !this.renewal.days) ||
      !this.renewal.simultaneousUserCount
    ) {
      this.cost = '--';
      this.color = 'secondary';
      return;
    }

    this.renewal.accountId = this.user_service.accountId;

    this.estimate$.next(this.renewal);
  }

  private async loadLastPlan() {
    this.current_user = await this.user_service.user();
    this.service.info(this.user_service.accountId).subscribe((plan) => {
      if (!plan) return;
      this.renewal = plan;
      this.requiresRealmChoice = plan.requiresRealmChoice;
      this.renewal.realmId = undefined;

      if (this.maxUserCounts.includes(plan.simultaneousUserCount)) {
        this.selectedUserCount = plan.simultaneousUserCount;
      }

      if (this.timeChoises.includes(plan.days)) {
        this.selectedTime = plan.days;
      }

      if (this.trafficChoises.includes(plan.gigabytes)) {
        this.selectedTraffic = plan.gigabytes;
      }

      if (
        (!this.renewal.gigabytes && !this.renewal.days) ||
        !this.renewal.simultaneousUserCount
      ) {
        this.color = 'primary';
      }

      this.fetchEstimate();
    });
  }

  private loadPrcies() {
    this.service.prices().subscribe((prices) => (this.prices = prices));
  }
}
