import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
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
  OperatingSystem,
  ResultStatus,
  SaveServer,
  ServerFeature,
  ServerRow,
} from '../../@models';
import {
  MessageService,
  TranslationPipe,
  TranslationService,
} from '../../@services';
import { ServersService } from './servers.service';

@Component({
  selector: 'app-servers',
  imports: [
    CommonModule,
    FormsModule,
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
    FormCheckInputDirective,
    FormCheckLabelDirective,
    ModalComponent,
    ModalHeaderComponent,
    ModalTitleDirective,
    ModalBodyComponent,
    ModalFooterComponent,
    ButtonCloseDirective,
    MatTableModule,
  ],
  templateUrl: './servers.component.html',
  styleUrl: './servers.component.scss',
})
export class ServersComponent implements OnInit {
  readonly icons = ICON_SUBSET;
  readonly os_types = [OperatingSystem.Mikrotik, OperatingSystem.Ubuntu];
  readonly feature_flags = [
    ServerFeature.RadiusDesk,
    ServerFeature.UserManager,
    ServerFeature.Xui,
    ServerFeature.Ikev2,
    ServerFeature.Sstp,
    ServerFeature.Ovpn,
    ServerFeature.V2ray,
  ];

  rows: ServerRow[] = [];
  loading = false;

  edit_visible = false;
  edit_id = 0;
  busy = false;

  // form state
  name = '';
  ip_address = '';
  domain_name = '';
  realm_id?: number;
  band_width?: number;
  os_type: OperatingSystem = OperatingSystem.Mikrotik;
  feature_state: Record<number, boolean> = {};
  config_text = '';

  constructor(
    private readonly service: ServersService,
    private readonly messages: MessageService,
    private readonly translation: TranslationService,
  ) {}

  get displayedColumns(): string[] {
    return [
      'name',
      'ip',
      'domain',
      'realm',
      'bandwidth',
      'os',
      'features',
      'config',
      'status',
      'created',
      'actions',
    ];
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.service.list().subscribe({
      next: (rows) => {
        this.loading = false;
        this.rows = rows ?? [];
      },
      error: () => (this.loading = false),
    });
  }

  // --- create / update modal (write-without-read config) ---

  openCreate(): void {
    this.edit_id = 0;
    this.resetForm();
    this.edit_visible = true;
  }

  openEdit(row: ServerRow): void {
    this.edit_id = row.id;
    this.resetForm();
    this.name = row.name;
    this.ip_address = row.ipAddress;
    this.domain_name = row.domainName;
    this.realm_id = row.realmId;
    this.band_width = row.bandWidth;
    this.os_type = row.osType;
    for (const flag of this.feature_flags) {
      this.feature_state[flag] = this.hasFeature(row.features, flag);
    }
    // the stored (protected) config is never read back — an empty box keeps it
    this.config_text = '';
    this.edit_visible = true;
  }

  submit(): void {
    if (this.busy) return;

    const model: SaveServer = {
      id: this.edit_id,
      realmId: this.realm_id ?? 0,
      ipAddress: this.ip_address.trim(),
      name: this.name.trim(),
      domainName: this.domain_name.trim(),
      bandWidth: this.band_width ?? 0,
      osType: this.os_type,
      features: this.selectedFeatures(),
    };

    const config_text = this.config_text.trim();
    if (config_text.length > 0) {
      try {
        model.config = JSON.parse(config_text);
      } catch {
        this.messages.messageHandler(
          ResultStatus.error,
          this.translation.translate('management.servers.modals.edit-title'),
          this.translation.translate('management.servers.messages.invalid-config'),
        );
        return;
      }
    }

    this.busy = true;
    const job = this.edit_id === 0
      ? this.service.create(model)
      : this.service.update(model);

    job.subscribe((result) => {
      this.busy = false;
      if (result.status() !== ResultStatus.success) return;

      this.edit_visible = false;
      this.load();
    });
  }

  toggleActive(row: ServerRow): void {
    if (this.busy) return;

    this.busy = true;
    this.service.setActive(row.id, !row.isActive).subscribe((result) => {
      this.busy = false;
      if (result.status() !== ResultStatus.success) return;

      this.load();
    });
  }

  // --- helpers ---

  hasFeature(features: ServerFeature, flag: ServerFeature): boolean {
    return (features & flag) === flag;
  }

  selectedFeatures(): ServerFeature {
    let value = 0;
    for (const flag of this.feature_flags) {
      if (this.feature_state[flag]) value |= flag;
    }
    return value;
  }

  featureTitle(flag: ServerFeature): string {
    return this.translation.translate(
      'management.servers.features.' + ServerFeature[flag],
    );
  }

  featureTitles(row: ServerRow): string[] {
    return this.feature_flags
      .filter((flag) => this.hasFeature(row.features, flag))
      .map((flag) => this.featureTitle(flag));
  }

  osTitle(os: OperatingSystem): string {
    return this.translation.translate(
      'management.servers.os.' + OperatingSystem[os],
    );
  }

  private resetForm(): void {
    this.name = '';
    this.ip_address = '';
    this.domain_name = '';
    this.realm_id = undefined;
    this.band_width = undefined;
    this.os_type = OperatingSystem.Mikrotik;
    this.feature_state = {};
    this.config_text = '';
  }
}
