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
  PlanEstimate,
  PlanInfo,
  PlanType,
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
  readonly monthlyChoises = [1, 2, 3, 4, 5, 6];
  readonly trafficChoises = [25, 50, 75, 100, 150];

  readonly monthlyUnit!: string;
  readonly trafficUnit!: string;

  color: string = 'secondary';
  selectedMonthly = 0;
  selectedTraffic = 0;
  selectedUserCount = 0;
  cost: string = '--';
  valid = false;

  estimate = {} as PlanEstimate;
  current_user!: UserModel;
  prices?: PriceModel[];
  result?: RenewalResult;

  constructor(
    private readonly service: RenewalService,
    private readonly user_service: UserService,
    private readonly router: Router,
    translation: TranslationService
  ) {
    this.monthlyUnit = translation.translate('renewal.labels.monthly.unit');
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
    if (!this.estimate.value || !this.estimate.simultaneousUserCount) return;

    const plan = this.estimate as PlanInfo;
    
    plan.target =
      this.user_service.targetName ?? this.current_user.username;

    this.service.renewal(plan).subscribe(async (result) => {
      this.result = result;

      if (result.moneyNeeds > 0) {
        setTimeout(() => this.router.navigate(['payment']), 1000);
      } else {
        setTimeout(() => this.router.navigate(['dashboard']), 2000);
      }

      const current_user = await this.user_service.user();
      current_user.balance = result.currentPrice;
    });
  }

  setMonthly() {
    if (this.selectedMonthly === 0) return;
    this.color = 'info';
    this.estimate.type = PlanType.Monthly;
    this.estimate.value = this.selectedMonthly;
    this.selectedTraffic = 0;
    this.fetchEstimate();
  }

  setTraffic() {
    if (this.selectedTraffic === 0) return;
    this.color = 'warning';
    this.estimate.type = PlanType.Traffic;
    this.estimate.value = this.selectedTraffic;
    this.selectedMonthly = 0;
    this.fetchEstimate();
  }

  setUserCount() {
    this.estimate.simultaneousUserCount = this.selectedUserCount;
    this.fetchEstimate();
  }

  private fetchEstimate() {
    if (!this.estimate.value || !this.estimate.simultaneousUserCount) {
      this.cost = '--';
      this.valid = false;
      return;
    }

    this.service.estimate(this.estimate).subscribe((cost) => {
      this.valid = cost ? true : false;
      this.cost = printMoney(cost);
    });
  }

  private async loadLastPlan() {
    this.current_user = await this.user_service.user();
    this.service.info(this.user_service.targetName).subscribe((plan) => {
      if (!plan) return;
      this.estimate = plan;

      if (this.maxUserCounts.includes(plan.simultaneousUserCount)) {
        this.selectedUserCount = plan.simultaneousUserCount;
      }

      if (
        plan.type === PlanType.Monthly &&
        this.monthlyChoises.includes(plan.value)
      ) {
        this.color = 'info';
        this.selectedMonthly = plan.value;
      } else if (
        plan.type === PlanType.Traffic &&
        this.trafficChoises.includes(plan.value)
      ) {
        this.color = 'warning';
        this.selectedTraffic = plan.value;
      }

      this.fetchEstimate();
    });
  }

  private loadPrcies() {
    this.service.prices().subscribe((prices) => (this.prices = prices));
  }
}
