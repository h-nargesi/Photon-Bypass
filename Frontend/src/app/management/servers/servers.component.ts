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
  SpinnerComponent,
  TooltipDirective,
} from '@coreui/angular';
import { IconDirective } from '@coreui/icons-angular';
import { ICON_SUBSET } from '../../@icons';
import {
  OperatingSystem,
  ResultStatus,
  SaveServer,
  ServerConnectionReport,
  ServerConnectionTest,
  ServerFeature,
  ServerRow,
  ServerTransport,
} from '../../@models';
import {
  MessageService,
  TranslationPipe,
  TranslationService,
  bytesToGigabytes,
  gigabytesToBytes,
  printCapacity,
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
    SpinnerComponent,
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
  capacity_gigabytes?: number;
  max_clients?: number;
  os_type: OperatingSystem = OperatingSystem.Mikrotik;
  feature_state: Record<number, boolean> = {};
  config_text = '';
  // Docs/25 D3: the transport lives inside the config JSON — the selector fills it in,
  // the box decides whenever it names one
  readonly transports = [ServerTransport.Direct, ServerTransport.SshTunnel];
  transport = ServerTransport.Direct;

  // connection test (Docs/25 D8–D9)
  test_visible = false;
  testing = false;
  test_state: 'running' | 'passed' | 'failed' | 'error' = 'running';
  test_report?: ServerConnectionReport;
  // a report of the unsaved form may write a discovered host key back into the config box
  test_from_form = false;

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
      'capacity',
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
    // exact value: saving an unrelated edit must give back the stored bytes
    this.capacity_gigabytes = bytesToGigabytes(row.capacityBytes);
    this.max_clients = row.maxClients ?? undefined;
    this.os_type = row.osType;
    for (const flag of this.feature_flags) {
      this.feature_state[flag] = this.hasFeature(row.features, flag);
    }
    // the stored (protected) config is never read back — an empty box keeps it;
    // its transport is part of that blob, so the selector starts at the default
    this.config_text = '';
    this.transport = ServerTransport.Direct;
    this.edit_visible = true;
  }

  submit(): void {
    if (this.busy) return;

    const parsed = this.parseConfig();
    if (!parsed.ok) return;

    this.busy = true;
    const job = this.edit_id === 0
      ? this.service.create(this.buildModel(parsed.config))
      : this.service.update(this.buildModel(parsed.config));

    job.subscribe((result) => {
      this.busy = false;
      if (result.status() !== ResultStatus.success) return;

      this.edit_visible = false;
      this.load();
    });
  }

  // --- connection test (Docs/25 D8–D9) ---

  /** Tests the unsaved form; nothing is saved. */
  testForm(): void {
    if (this.testing) return;

    const parsed = this.parseConfig();
    if (!parsed.ok) return;

    this.runTest(this.buildModel(parsed.config), true);
  }

  /** Row action: only the id goes, so the stored configuration is tested. */
  testRow(row: ServerRow): void {
    if (this.busy || this.testing) return;

    this.runTest({ id: row.id }, false);
  }

  stepTitle(step: string): string {
    return this.translation.translate('management.servers.test.steps.' + step);
  }

  transportTitle(transport: ServerTransport): string {
    return this.translation.translate('management.servers.transport.' + ServerTransport[transport]);
  }

  /** The selector writes the transport into the box; an empty box gets the tunnel template. */
  onTransportChange(): void {
    if (this.transport === ServerTransport.SshTunnel && this.config_text.trim().length === 0) {
      this.config_text = JSON.stringify(TUNNEL_CONFIG_TEMPLATE, null, 2);
      return;
    }

    // an invalid box is left alone: the submit reports it
    const config = this.readConfig();

    if (!config || config === INVALID_CONFIG) return;

    if (this.transport === ServerTransport.SshTunnel) config['transport'] = TUNNEL_TRANSPORT;
    else delete config['transport'];

    this.config_text = JSON.stringify(config, null, 2);
  }

  /** The box decides: a config that names its transport moves the selector. */
  onConfigChange(): void {
    const config = this.readConfig();

    const named = config === INVALID_CONFIG ? undefined : config?.['transport'];

    if (named === 'Direct') this.transport = ServerTransport.Direct;
    else if (named === 'SshTunnel') this.transport = ServerTransport.SshTunnel;
  }

  get can_apply_host_key(): boolean {
    return this.test_from_form && !!this.test_report?.hostKeySha256;
  }

  /** Writes the discovered host key into the config box (Docs/25 D8: the pin is learned by the test). */
  applyHostKey(): void {
    // the stored config is never read back: only a form test has a box to write into
    if (!this.can_apply_host_key) return;

    // an empty box starts a new tunnel config; an invalid one is reported and left alone
    const config = this.readConfig(true) ?? {};

    if (config === INVALID_CONFIG) return;

    const ssh = config['sshConfig'];

    config['transport'] = TUNNEL_TRANSPORT;
    config['sshConfig'] = {
      ...(ssh && typeof ssh === 'object' ? ssh : {}),
      hostKeySha256: this.test_report?.hostKeySha256,
    };
    this.config_text = JSON.stringify(config, null, 2);
    this.transport = ServerTransport.SshTunnel;
  }

  private runTest(model: ServerConnectionTest, from_form: boolean): void {
    this.testing = true;
    this.test_from_form = from_form;
    this.test_report = undefined;
    this.test_state = 'running';
    this.test_visible = true;

    this.service.testConnection(model).subscribe((result) => {
      this.testing = false;

      if (result.status() !== ResultStatus.success) {
        this.test_state = 'error';
        return;
      }

      this.test_report = result.data ?? undefined;
      this.test_state = this.test_report?.passed ? 'passed' : 'failed';
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

  // T4: the client-count cap only applies to x-ui rows — the field is x-ui-only
  get is_xui(): boolean {
    return !!this.feature_state[ServerFeature.Xui];
  }

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

  capacityTitle(row: ServerRow): string {
    return printCapacity(row.capacityBytes);
  }

  /**
   * The config box as an object, `undefined` when it is empty, or
   * {@link INVALID_CONFIG} when it is not a JSON object (a message is shown
   * only when `report` is set).
   */
  private readConfig(report = false): Record<string, unknown> | undefined {
    const text = this.config_text.trim();

    if (text.length === 0) return undefined;

    try {
      const parsed: unknown = JSON.parse(text);

      if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) throw new Error('not an object');

      return parsed as Record<string, unknown>;
    } catch {
      if (report) {
        this.messages.messageHandler(
          ResultStatus.error,
          this.translation.translate('management.servers.modals.edit-title'),
          this.translation.translate('management.servers.messages.invalid-config'),
        );
      }

      return INVALID_CONFIG;
    }
  }

  /** The payload config: absent when the box is empty (an update keeps the stored blob), never sent when it is invalid. */
  private parseConfig(): { ok: boolean; config?: Record<string, unknown> } {
    const config = this.readConfig(true);

    if (config === INVALID_CONFIG) return { ok: false };

    if (config === undefined) return { ok: true };

    // the selector only completes a config that names no transport
    if (this.transport === ServerTransport.SshTunnel && config['transport'] === undefined) {
      config['transport'] = TUNNEL_TRANSPORT;
    }

    return { ok: true, config };
  }

  private buildModel(config?: Record<string, unknown>): SaveServer {
    const model: SaveServer = {
      id: this.edit_id,
      realmId: this.realm_id ?? 0,
      ipAddress: this.ip_address.trim(),
      name: this.name.trim(),
      domainName: this.domain_name.trim(),
      capacityBytes: gigabytesToBytes(this.capacity_gigabytes),
      maxClients: this.is_xui ? (this.max_clients ?? null) : null,
      osType: this.os_type,
      features: this.selectedFeatures(),
    };

    if (config !== undefined) model.config = config;

    return model;
  }

  private resetForm(): void {
    this.name = '';
    this.ip_address = '';
    this.domain_name = '';
    this.realm_id = undefined;
    this.capacity_gigabytes = undefined;
    this.max_clients = undefined;
    this.os_type = OperatingSystem.Mikrotik;
    this.feature_state = {};
    this.config_text = '';
    this.transport = ServerTransport.Direct;
  }
}

/** Starting point of a tunnel config (Docs/25 §4.1, §4.6): every field the tunnel transport needs. */
const TUNNEL_TRANSPORT = ServerTransport[ServerTransport.SshTunnel];

/** Sentinel of a config box that is not a JSON object (distinct from an empty box). */
const INVALID_CONFIG: Record<string, unknown> = {};

const TUNNEL_CONFIG_TEMPLATE = {
  transport: TUNNEL_TRANSPORT,
  sshConfig: {
    username: '',
    port: 22,
    privateKey: '-----BEGIN OPENSSH PRIVATE KEY-----\n...\n-----END OPENSSH PRIVATE KEY-----',
    hostKeySha256: 'SHA256:',
  },
  webApiConfig: {
    hostName: '127.0.0.1',
    port: 2053,
    username: '',
    password: '',
  },
};
