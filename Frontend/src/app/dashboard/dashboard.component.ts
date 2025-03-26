import { CommonModule, NgStyle } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  ButtonDirective,
  CardBodyComponent,
  CardComponent,
  CardTextDirective,
  CardTitleDirective,
  ColComponent,
  ColDirective,
  Colors,
  ListGroupDirective,
  ListGroupItemDirective,
  PlaceholderAnimationDirective,
  PlaceholderDirective,
  ProgressComponent,
  RowComponent,
  SpinnerComponent,
  TemplateIdDirective,
  TextColorDirective,
  TooltipDirective,
  WidgetStatCComponent,
} from '@coreui/angular';
import { IconDirective } from '@coreui/icons-angular';
import { ICON_SUBSET } from '../@icons';
import { PlanType, ResultStatus, Target, UserModel, UserPlanInfo } from '../@models';
import { TranslationPipe, TranslationService, UserService } from '../@services';
import { DashboardService } from './dashboard.service';
import { TrafficChartComponent } from './traffic-chart/traffic-chart.component';

@Component({
  selector: 'app-dashboard',
  imports: [
    NgStyle,
    CommonModule,
    RowComponent,
    ColComponent,
    CardComponent,
    CardBodyComponent,
    ColDirective,
    IconDirective,
    TooltipDirective,
    ButtonDirective,
    CardTextDirective,
    CardTitleDirective,
    ListGroupDirective,
    ListGroupItemDirective,
    WidgetStatCComponent,
    TranslationPipe,
    PlaceholderDirective,
    PlaceholderAnimationDirective,
    RouterLink,
    SpinnerComponent,
    TemplateIdDirective,
    ProgressComponent,
    TrafficChartComponent,
    TextColorDirective,
  ],
  providers: [DashboardService],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  current_user?: UserModel;
  sending_cert_email = false;

  connections?: number[];
  connection_count = '0';
  closing_connection: boolean[] = [];

  plan_info?: UserPlanInfo;
  readonly icons = ICON_SUBSET;

  constructor(
    private readonly user_service: UserService,
    private readonly service: DashboardService,
    private readonly translation: TranslationService
  ) {}

  get targetName(): string | undefined {
    return this.user_service.targetName;
  }

  get targetUser(): Target | undefined {
    return this.user_service.targetUser;
  }

  async ngOnInit() {
    await this.loadCurrentUser();
    this.loadData();
    this.detectTargetChanges();
  }

  showTagetInUserPanel(): boolean {
    return (
      this.user_service.hasSubUsers &&
      this.targetName !== this.current_user?.username
    );
  }

  sendCertificateViaEmail(toMe: boolean): void {
    this.sending_cert_email = true;
    this.service
      .sendCertificateViaEmail(
        toMe ? this.current_user?.username : this.user_service.targetName
      )
      .subscribe(() => (this.sending_cert_email = false));
  }

  closeConnection(index: number) {
    this.closing_connection[index] = true;
    const targte = this.user_service.targetName;
    this.service.closeConnection(index, targte).subscribe((result) => {
      if (result.status() >= ResultStatus.warning) return;

      if (this.connections && this.connections.length > index) {
        this.closing_connection.splice(index, 1);
        this.connections.splice(index, 1);
      }
    });
  }

  printDuration(duration: number): string {
    const hours = Math.floor(duration / 60);
    const minutes = duration % 60;
    const unit = this.translation.translate(
      'dashboard.connections.' + (hours > 0 ? 'hour' : 'minute')
    );
    let value = '';
    if (hours < 1) value = minutes.toString();
    else if (minutes < 1) value = hours.toString();
    else value = `${hours}:${('0' + minutes).slice(-2)}`;

    return this.translation.translate('dashboard.connections.duration', [
      value,
      unit,
    ]);
  }

  getPlanInfoColor(plan_type?: PlanType): Colors {
    switch (plan_type) {
      case PlanType.Monthly:
        return 'info';
      case PlanType.Traffic:
        return 'warning';
      default:
        return 'dark';
    }
  }

  getPlanInfoIcon(plan_type?: PlanType): string[] | undefined {
    switch (plan_type) {
      case PlanType.Monthly:
        return this.icons.cilAvTimer;
      case PlanType.Traffic:
        return this.icons.cilChartPie;
      default:
        return undefined;
    }
  }

  getPlanInfoTooltip(plan_type?: PlanType): string | undefined {
    switch (plan_type) {
      case PlanType.Monthly:
        return this.translation.translate('dashboard.balance.monthly');
      case PlanType.Traffic:
        return this.translation.translate('dashboard.balance.traffic');
      default:
        return undefined;
    }
  }

  private loadData() {
    this.loadConnections();
    this.loadPlanInfo();
  }

  private async loadCurrentUser() {
    this.current_user = await this.user_service.user();
  }

  private loadConnections() {
    this.service
      .fetchCurrentConnections(this.user_service.targetName)
      .subscribe((connections) => {
        this.connections = connections;

        this.connection_count = (connections?.length ?? 0).toString();

        this.closing_connection = [];
        if (connections) {
          for (let i = 0; i < connections.length; i++)
            this.closing_connection.push(false);
        }
      });
  }

  private loadPlanInfo() {
    this.service
      .fetchPlanState(this.user_service.targetName)
      .subscribe((info) => (this.plan_info = info));
  }

  private detectTargetChanges() {
    this.user_service.onTargetChanged.subscribe((newTarget) => {
      this.clearData();
      this.loadData();
    });
  }

  private clearData() {
    this.connections = undefined;
    this.connection_count = '0';
    this.closing_connection = [];
    this.plan_info = undefined;
  }
}
