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
}

/// API row of the admin server list (backend `ServerRowModel`, docs/20 phase 5).
/// The stored config itself never leaves the server — only the has-config flag.
export interface ServerRow {
  id: number;
  isActive: boolean;
  realmId: number;
  realm?: string;
  ipAddress: string;
  name: string;
  domainName: string;
  bandWidth: number;
  osType: OperatingSystem;
  features: ServerFeature;
  hasConfig: boolean;
  created: number;
  createdTitle: string;
}

/// Admin server create/update payload (backend `SaveServerModel`, docs/20 phase 5).
/// Id = 0 means create; a null config on update keeps the stored protected blob.
export interface SaveServer {
  id: number;
  realmId: number;
  ipAddress: string;
  name: string;
  domainName: string;
  bandWidth: number;
  osType: OperatingSystem;
  features: ServerFeature;
  config?: unknown;
}
