import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
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
  RenewalContext,
  PriceModel,
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

  get targetName(): string | undefined {
    return this.user_service.targetName;
  }

  ngOnInit() {
    this.loadLastPlan();
    this.loadPrcies();
  }

  submit() {
    if (
      (!this.renewal.gigabytes && !this.renewal.days) ||
      !this.renewal.simultaneousUserCount
    ) {
      return;
    }

    this.renewal.target =
      this.user_service.targetName ?? this.current_user.username;

    this.service.renewal(this.renewal).subscribe(async (result) => {
      this.result = result;

      console.log(result);
      if (result.moneyNeeds > 0) {
        setTimeout(() => this.router.navigate(['payment'], {
          queryParams: {
            invoice: "10"
          }
        }), 1000);
      } else {
        setTimeout(() => this.router.navigate(['dashboard']), 2000);
      }

      const current_user = await this.user_service.user();
      current_user.balance = result.currentPrice;
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

    this.service.estimate(this.renewal).subscribe((cost) => {
      this.color = cost ? 'primary' : 'secondary';
      this.selectedTime = cost.days;
      this.cost = printMoney(cost.price);
    });
  }

  private async loadLastPlan() {
    this.current_user = await this.user_service.user();
    this.service.info(this.user_service.targetName).subscribe((plan) => {
      if (!plan) return;
      this.renewal = plan;

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
