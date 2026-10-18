import { CommonModule, NgStyle } from '@angular/common';
import { HttpErrorResponse, HttpResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
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
import {
  ConnectionStateModel,
  PlanInfo,
  ResultStatus,
  Target,
  UserModel,
  PlanState,
  VpnConfigInfo,
} from '../@models';
import {
  MessageService,
  TranslationPipe,
  TranslationService,
  UserService,
} from '../@services';
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
  downloading_cert = false;
  copying_vless_link = false;

  connections?: ConnectionStateModel[];
  connection_count = '0';

  plan_info?: PlanState;
  plan_capabilities?: PlanInfo;
  config_info?: VpnConfigInfo;
  readonly icons = ICON_SUBSET;

  constructor(
    private readonly user_service: UserService,
    private readonly service: DashboardService,
    private readonly translation: TranslationService,
    private readonly messages: MessageService
  ) {}

  private readonly destroy_ref = inject(DestroyRef);

  get targetName(): string | undefined {
    return this.user_service.targetName;
  }

  get targetUser(): Target | undefined {
    return this.user_service.targetUser;
  }

  get supportsCredentialRotation(): boolean {
    return this.plan_capabilities?.supportsCredentialRotation === true;
  }

  get supportsConfigDelivery(): boolean {
    return (
      this.plan_capabilities?.supportsOvpnCert === true ||
      this.supportsCredentialRotation
    );
  }

  get vlessLink(): string | undefined {
    return this.config_info?.vlessLink ?? undefined;
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

  downloadCertificate(): void {
    this.downloading_cert = true;
    this.service
      .downloadCertificate(this.user_service.targetName)
      .subscribe({
        next: (response) => {
          this.downloading_cert = false;
          this.saveCertificateFile(response);
        },
        error: (error: HttpErrorResponse) => {
          this.downloading_cert = false;
          void this.reportDownloadError(error);
        },
      });
  }

  closeConnection(index: number) {
    if (!this.connections || this.connections.length < 1) return;
    if (index < 0 || index >= this.connections.length) return;

    const target = this.user_service.targetName;
    const connection = this.connections[index];

    connection.closing = true;

    this.service
      .closeConnection(connection.server, connection.sessionId, target)
      .subscribe((result) => {
        if (result.status() >= ResultStatus.warning) return;

        if (this.connections) {
          this.connections.splice(this.connections.indexOf(connection), 1);
          this.connection_count = this.connections.length.toString();
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

  getPlanInfoColor(plan_info?: PlanState): Colors {
    if (!plan_info) return 'dark';
    else if (plan_info.remainsTimePercent > plan_info.remainsTrafficPercent) {
      return 'warning';
    } else {
      return 'info';
    }
  }

  getPlanInfoIcon(plan_info?: PlanState): string[] | undefined {
    if (!plan_info) return undefined;
    else if (plan_info.remainsTimePercent > plan_info.remainsTrafficPercent) {
      return this.icons.cilAvTimer;
    } else {
      return this.icons.cilChartPie;
    }
  }

  getPlanInfoTooltip(plan_info?: PlanState): string | undefined {
    if (!plan_info) return undefined;
    else if (plan_info.remainsTimePercent > plan_info.remainsTrafficPercent) {
      return this.translation.translate('dashboard.balance.monthlyRisk');
    } else {
      return this.translation.translate('dashboard.balance.trafficRisk');
    }
  }

  private loadData() {
    this.loadConnections();
    this.loadPlanInfo();
    this.loadPlanCapabilities();
  }

  private async loadCurrentUser() {
    this.current_user = await this.user_service.user();
  }

  copyVlessLink(): void {
    const link = this.vlessLink;
    if (!link || this.copying_vless_link) return;

    this.copying_vless_link = true;

    const done = (success: boolean) => {
      this.copying_vless_link = false;
      this.messages.messageHandler(
        success ? ResultStatus.success : ResultStatus.error,
        this.translation.translate('dashboard.account.copy-vless-link'),
        success
          ? this.translation.translate('dashboard.account.vless-link-copied')
          : this.translation.translate('api.error'),
      );
    };

    if (!navigator.clipboard) {
      done(false);
      return;
    }

    navigator.clipboard
      .writeText(link)
      .then(() => done(true))
      .catch(() => done(false));
  }

  private loadConnections() {
    this.service
      .fetchCurrentConnections(this.user_service.targetName)
      .subscribe((connections) => {
        this.connections = connections;
        this.connection_count = (connections?.length ?? 0).toString();

        if (connections) {
          for (const con of this.connections) con.closing = false;
        }
      });
  }

  private loadPlanInfo() {
    this.service
      .fetchPlanState(this.user_service.targetName)
      .subscribe((info) => (this.plan_info = info));
  }

  private loadPlanCapabilities() {
    this.service
      .fetchPlanInfo(this.user_service.targetName)
      .subscribe(info => {
        this.plan_capabilities = info;

        if (info?.supportsCredentialRotation) this.loadConfigInfo();
      });
  }

  private loadConfigInfo() {
    this.service
      .fetchConfigInfo(this.user_service.targetName)
      .subscribe(info => (this.config_info = info));
  }

  private saveCertificateFile(response: HttpResponse<Blob>): void {
    if (!response.ok || !response.body) return;

    const file_name = this.fileDispositionName(response) ?? `${this.user_service.targetName}.zip`;
    const download_url = URL.createObjectURL(response.body);
    const anchor = document.createElement('a');
    anchor.href = download_url;
    anchor.download = file_name;
    anchor.click();
    URL.revokeObjectURL(download_url);
  }

  private fileDispositionName(response: HttpResponse<Blob>): string | undefined {
    const type = response.headers.get('Content-Disposition') ?? '';
    return /filename="?([^";]+)"?/i.exec(type)?.[1];
  }

  private async reportDownloadError(error: HttpErrorResponse): Promise<void> {
    let message: string | undefined;

    if (error.error instanceof Blob) {
      try {
        message = JSON.parse(await error.error.text())?.message;
      } catch {
        message = undefined;
      }
    }

    this.messages.messageHandler(
      ResultStatus.error,
      this.translation.translate('api.error'),
      message ?? this.translation.translate('api.error')
    );
  }

  private detectTargetChanges() {
    this.user_service.onTargetChanged
      .pipe(takeUntilDestroyed(this.destroy_ref))
      .subscribe((newTarget) => {
        this.clearData();
        this.loadData();
      });
  }

  private clearData() {
    this.connections = undefined;
    this.connection_count = '0';
    this.plan_info = undefined;
    this.plan_capabilities = undefined;
    this.config_info = undefined;
  }
}
