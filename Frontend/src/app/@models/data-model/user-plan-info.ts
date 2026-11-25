export interface PlanState {
  remainsTitle: string;
  remainsTrafficPercent: number;
  remainsTimePercent: number;
  simultaneousUserCount: number;
}

/// Docs/22 target contract: per-account calls name the VPN account by id;
/// `target` stays the (optional) username for manager on-behalf flows.
export interface AccountAction {
  accountId?: number;
  target?: string;
}

export interface RenewalContext extends AccountAction {
  days: number;
  gigabytes: number;
  simultaneousUserCount: number;
  /// realm re-pick on a stale renewal (Docs/22 D6)
  realmId?: number;
}

/// First purchase payload (Docs/22 D9): realm + username + plan.
export interface NewAccountContext {
  username?: string;
  realmId?: number;
  days: number;
  gigabytes: number;
  simultaneousUserCount: number;
}

/// One selectable realm of the purchase flow (Docs/22 D4). `title` is the only
/// realm text customers see; `name` stays the system identifier (Docs/24 D6).
export interface RealmChoice {
  id: number;
  name: string;
  title: string;
  isSelectable: boolean;
}

export interface PlanInfo {
  target: string;
  accountId: number;
  realmId: number;
  requiresRealmChoice: boolean;
  days: number;
  gigabytes: number;
  simultaneousUserCount: number;
  kind?: string | null;
  supportsOvpnCert?: boolean | null;
  supportsPasswordChange?: boolean | null;
  supportsCredentialRotation?: boolean | null;
}

export interface VpnConfigInfo {
  kind?: string | null;
  vlessLink?: string | null;
}

export interface RenewalResult {
  currentPrice: number;
  invoiceCode?: number;
}

export interface EstimateResult {
  price: number;
  days: number;
  gigabytes: number;
  simultaneousUserCount: number;
}

export interface PaymentInvoiceItem {
  title: string;
  value: number;
}

export interface PaymentCard {
  bankName?: string;
  cardNumber?: string;
  holderName?: string;
}

export interface PaymentInvoice {
  code: number;
  kind: number;
  status: number;
  totalPrice: number;
  walletDeduction: number;
  payable: number;
  walletBalance: number;
  allowWallet: boolean;
  hasReceipt: boolean;
  items: PaymentInvoiceItem[];
  cardInfo: PaymentCard[];
}
