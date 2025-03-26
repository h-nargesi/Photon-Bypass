import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  BorderDirective,
  ButtonDirective,
  CardBodyComponent,
  CardComponent,
  CardHeaderComponent,
  ColComponent,
  FormSelectDirective,
  PlaceholderAnimationDirective,
  PlaceholderDirective,
  RowComponent,
} from '@coreui/angular';
import {
  PlanInto,
  PlanType,
  PriceModel,
  ResultStatus,
  UserModel,
} from '../@models';
import {
  printMoney,
  TranslationPipe,
  TranslationService,
  UserService,
} from '../@services';
import { RnewalService } from './rnewal.service';

@Component({
  selector: 'app-rnewal',
  imports: [
    CommonModule,
    FormsModule,
    RowComponent,
    ColComponent,
    CardComponent,
    CardHeaderComponent,
    CardBodyComponent,
    ButtonDirective,
    BorderDirective,
    FormSelectDirective,
    PlaceholderDirective,
    PlaceholderAnimationDirective,
    TranslationPipe,
  ],
  templateUrl: './rnewal.component.html',
  styleUrl: './rnewal.component.scss',
  providers: [RnewalService],
})
export class RnewalComponent implements OnInit {
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

  plan = {} as PlanInto;
  current_user!: UserModel;
  prices?: PriceModel[];

  constructor(
    private readonly service: RnewalService,
    private readonly user_service: UserService,
    private readonly router: Router,
    translation: TranslationService
  ) {
    this.monthlyUnit = translation.translate('rnewal.labels.monthly.unit');
    this.trafficUnit = translation.translate('rnewal.labels.traffic.unit');
  }

  get target(): string {
    return this.user_service.Target;
  }

  ngOnInit() {
    this.loadLastPlan();
    this.loadPrcies();
  }

  submit() {
    if (!this.plan.value || !this.plan.simultaneousUserCount) return;

    this.plan.target = this.user_service.Target ?? this.current_user.username;

    this.service.rnewal(this.plan).subscribe((result) => {
      if (result.code === 307) {
        setTimeout(() => this.router.navigate(['/payment']), 1000);
      } else if (result.status() === ResultStatus.success) {
        setTimeout(() => this.router.navigate(['/dashboard']), 2000);
      }
    });
  }

  setMonthly() {
    if (this.selectedMonthly === 0) return;
    this.color = 'info';
    this.plan.type = PlanType.Monthly;
    this.plan.value = this.selectedMonthly;
    this.selectedTraffic = 0;
    this.fetchEstimate();
  }

  setTraffic() {
    if (this.selectedTraffic === 0) return;
    this.color = 'warning';
    this.plan.type = PlanType.Traffic;
    this.plan.value = this.selectedTraffic;
    this.selectedMonthly = 0;
    this.fetchEstimate();
  }

  setUserCount() {
    this.plan.simultaneousUserCount = this.selectedUserCount;
    this.fetchEstimate();
  }

  private fetchEstimate() {
    if (!this.plan.value || !this.plan.simultaneousUserCount) {
      this.cost = '--';
      this.valid = false;
      return;
    }

    this.plan.target = this.user_service.Target ?? this.current_user.username;

    this.service.estimate(this.plan).subscribe((cost) => {
      this.valid = cost ? true : false;
      this.cost = printMoney(cost);
    });
  }

  private async loadLastPlan() {
    this.current_user = await this.user_service.user();
    this.service.info(this.user_service.Target).subscribe((plan) => {
      if (!plan) return;
      this.plan = plan;

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
