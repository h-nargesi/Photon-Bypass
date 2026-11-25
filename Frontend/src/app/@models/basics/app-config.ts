/// Public, unauthenticated frontend configuration (backend `AppConfigModel`, Docs/20 D11).
export interface AppConfig {
  allowRegistration: boolean;
  supportContactInfo?: string | null;
  supportDescription?: string | null;
}
