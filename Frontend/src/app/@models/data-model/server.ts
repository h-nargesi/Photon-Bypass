export enum OperatingSystem {
  Mikrotik = 1,
  Ubuntu = 2,
}

export enum ServerFeature {
  RadiusDesk = 0x1,
  UserManager = 0x2,
  Xui = 0x4,
  Ikev2 = 0x100,
  Sstp = 0x200,
  Ovpn = 0x400,
  V2ray = 0x800,
  L2tp = 0x1000,
}

/// How the panel reaches a server's management services (backend `ServerTransport`, Docs/25 D3):
/// `Direct` addresses the configured host, `SshTunnel` rides a pooled SSH session.
export enum ServerTransport {
  Direct = 0,
  SshTunnel = 1,
}

/// One step of the admin connection test (backend `ConnectionStepModel`, Docs/25 D9), in the order it ran.
export interface ConnectionStep {
  step: 'tcp' | 'host-key' | 'auth' | 'forward' | 'service';
  passed: boolean;
  /// Ports, durations, host-key fingerprints and general text only — never a key, password or cookie.
  detail?: string | null;
}

/// Answer of the admin connection test (backend `ServerConnectionReport`).
export interface ServerConnectionReport {
  passed: boolean;
  steps: ConnectionStep[];
  /// The host key the server presented; for registration when the stored pin is empty.
  hostKeySha256?: string | null;
  machineName: string;
}

/// Connection-test payload: the unsaved form (its config wins) or only an id, which tests the stored config.
export interface ServerConnectionTest {
  id: number;
  ipAddress?: string;
  name?: string;
  features?: ServerFeature;
  osType?: OperatingSystem;
  config?: unknown;
}

/// API row of the admin server list (backend `ServerRowModel`, Docs/20 phase 5).
/// The stored config itself never leaves the server — only the has-config flag.
export interface ServerRow {
  id: number;
  isActive: boolean;
  realmId: number;
  realm?: string;
  ipAddress: string;
  name: string;
  domainName: string;
  /// 30-day sellable traffic in BYTES (Docs/24 D1). The admin form converts to/from
  /// binary gigabytes (1024³); the wire and the database stay in bytes.
  capacityBytes: number;
  /// T4: optional hard client-count cap (x-ui capacity gate). Null/undefined = gate off.
  maxClients?: number | null;
  osType: OperatingSystem;
  features: ServerFeature;
  hasConfig: boolean;
  created: number;
  createdTitle: string;
}

/// Admin server create/update payload (backend `SaveServerModel`, Docs/20 phase 5).
/// Id = 0 means create; a null config on update keeps the stored protected blob.
export interface SaveServer {
  id: number;
  realmId: number;
  ipAddress: string;
  name: string;
  domainName: string;
  /// 30-day sellable traffic in BYTES (1 .. 1 PiB) — send `gigabytesToBytes(form)`.
  capacityBytes: number;
  /// T4: optional hard client-count cap — only meaningful on x-ui rows; null = gate off.
  maxClients?: number | null;
  osType: OperatingSystem;
  features: ServerFeature;
  config?: unknown;
}
