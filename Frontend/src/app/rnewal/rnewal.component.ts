import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  BorderDirective,
  ButtonDirective,
  CardBodyComponent,
  CardComponent,
  CardHeaderComponent,
  ColComponent,
  FormSelectDirective,
  RowComponent,
} from '@coreui/angular';
import { PlanInto, PlanType, UserModel } from '../@models';
import { printMoney, TranslationPipe, TranslationService, UserService } from '../@services';
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
    TranslationPipe,
  ],
  templateUrl: './rnewal.component.html',
  styleUrl: './rnewal.component.scss',
  providers: [RnewalService],
})
export class RnewalComponent {
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

  constructor(
    private readonly service: RnewalService,
    translation: TranslationService,
    user_service: UserService
  ) {
    this.monthlyUnit = translation.translate('rnewal.labels.monthly.unit');
    this.trafficUnit = translation.translate('rnewal.labels.traffic.unit');
    user_service
      .user()
      .subscribe((user_info) => (this.current_user = user_info));
  }

  submit() {

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
    if (!this.plan.type) {
      this.cost = '--';
      this.valid = false;
      return;
    }

    this.service.estimate(this.plan).subscribe((cost) => {
      this.valid = cost ? true : false;
      this.cost = printMoney(cost);
    });
  }
}
