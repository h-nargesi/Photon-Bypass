import { Injectable } from '@angular/core';
import { catchError, firstValueFrom, map, Observable, of, Subject } from 'rxjs';
import { AccountRole, Target, UserModel, UserVpnAccount } from '../../@models';
import { ApiBaseService } from '../api-services/api-base-service';
import { ACCOUNT_API_URL, VPN_API_URL } from '../api-services/models/app-api-url';
import { LocalStorageService } from '../local-storage/local-storage-service';
import { TranslationService } from '../translation/translation-service';

@Injectable({ providedIn: 'root' })
export class UserService extends ApiBaseService {
  private invoice_code?: string;
  private reload_next_call = false;
  private current_user?: UserModel;
  private loading_user?: Promise<UserModel>;
  private currentTargetUser?: Target;
  private targetUserEventSubject = new Subject<Target | undefined>();
  private currentAccountId?: number;
  /// Undefined means "the acting user's own accounts from get-user".
  /// An array (including empty) is the VPN list of another panel user.
  private activeAccounts?: UserVpnAccount[];
  private accountLoadGeneration = 0;
  private pendingAccountLoad?: Promise<void>;
  private accountEventSubject = new Subject<UserVpnAccount | undefined>();

  onTargetChanged = this.targetUserEventSubject.asObservable();
  /// Docs/22 §3.5: fires whenever the account switcher picks another VPN
  /// account; per-account views reload on it.
  onAccountChanged = this.accountEventSubject.asObservable();

  get targetUser(): Target | undefined {
    return this.currentTargetUser;
  }

  get accounts(): UserVpnAccount[] {
    return this.activeAccounts ?? this.current_user?.accounts ?? [];
  }

  get currentAccount(): UserVpnAccount | undefined {
    return this.accounts.find((account) => account.id === this.currentAccountId);
  }

  get accountId(): number | undefined {
    return this.currentAccount?.id ?? this.accounts[0]?.id;
  }

  get hasMultipleAccounts(): boolean {
    return this.accounts.length > 1;
  }

  /// The switcher is showing a panel user other than the one who logged in.
  get viewingOtherUser(): boolean {
    const target = this.currentTargetUser?.username;
    const self = this.current_user?.username;
    return !!target && !!self && target !== self;
  }

  get targetName(): string | undefined {
    return this.currentTargetUser?.username;
  }

  get hasSubUsers(): boolean {
    return this.current_user?.targetArea ? true : false;
  }

  get role(): AccountRole | undefined {
    return this.current_user?.role;
  }

  get isAdmin(): boolean {
    return this.current_user?.role === AccountRole.Admin;
  }

  get isManager(): boolean {
    return (this.current_user?.role ?? AccountRole.User) >= AccountRole.Manager;
  }

  /// D6 gate: only independent accounts (AllowSelfService) reach the
  /// self financial flows — the server enforces the same matrix (Docs/20 §3).
  get isSelfService(): boolean {
    return this.current_user?.allowSelfService === true;
  }

  get invoice(): string | undefined {
    return this.invoice_code;
  }

  set invoice(code: string) {
    this.invoice_code = code;
  }

  async user(): Promise<UserModel> {
    if (this.reload_next_call || this.current_user === undefined) {
      if (!this.reload_next_call && this.loading_user)
        return this.loading_user;

      this.loading_user = this.loadSession();

      try {
        return await this.loading_user;
      } finally {
        this.loading_user = undefined;
      }
    }

    return this.current_user;
  }

  /// Docs/22 §3.5: switch the active VPN account; per-account views react
  /// through onAccountChanged.
  setAccount(accountId: number | undefined) {
    if (!accountId || !this.accounts.some((account) => account.id === accountId)) {
      return;
    }

    if (this.currentAccountId === accountId) {
      return;
    }

    this.currentAccountId = accountId;
    this.writeStoredAccount(this.selectionOwner(), accountId);

    this.accountEventSubject.next(this.currentAccount);
  }

  setTraget(username: string | undefined) {
    if (!this.current_user?.targetArea) {
      this.currentTargetUser = undefined;
      LocalStorageService.set(['user', 'target'], undefined);
      throw 'can not set target to null!';
    }

    if (this.currentTargetUser?.username === username) {
      return;
    }

    if (!username || !(username in this.current_user.targetArea)) {
      username = this.current_user.username;
    }

    if (username in this.current_user.targetArea)
      this.currentTargetUser = this.current_user.targetArea[username];
    else throw 'target index is out of range';

    LocalStorageService.set(['user', 'target'], username);
    this.followTargetAccounts(true);

    this.targetUserEventSubject.next(this.currentTargetUser);
  }

  /// On-behalf target chosen from the admin user list (Docs/20 phase 6):
  /// the admin's targetArea only holds its own subordinates, while the
  /// server-side access check bypasses every target for the admin role.
  setExternalTarget(target: Target): boolean {
    if (!this.isAdmin || !target?.username) return false;

    if (this.currentTargetUser?.username === target.username) {
      return true;
    }

    this.currentTargetUser = target;
    LocalStorageService.set(['user', 'target'], target.username);
    this.followTargetAccounts(true);

    this.targetUserEventSubject.next(target);

    return true;
  }

  reload() {
    this.reload_next_call = true;
  }

  clear() {
    this.reload_next_call = false;
    this.loading_user = undefined;
    this.current_user = undefined;
    this.currentAccountId = undefined;
    this.activeAccounts = undefined;
    this.accountLoadGeneration++;
    this.pendingAccountLoad = undefined;
    LocalStorageService.set(['user', 'target'], undefined);
    LocalStorageService.set(['user', 'account'], undefined);
    LocalStorageService.set(['user', 'targetAccount'], undefined);
  }

  private async loadSession(): Promise<UserModel> {
    const refreshing = this.reload_next_call;

    try {
      this.current_user = await firstValueFrom(this.fetchUser());

      const previous_target = this.currentTargetUser?.username;
      this.setTraget(LocalStorageService.get(['user', 'target']));

      // Same target after a reload still needs a fresh list: get-user for
      // self, vpn/accounts for someone else. A changed target already loaded.
      if (
        refreshing &&
        this.currentTargetUser &&
        this.currentTargetUser.username === previous_target
      ) {
        this.followTargetAccounts(false);
      }

      if (!this.currentTargetUser) {
        this.useOwnAccounts(false);
      }

      await this.pendingAccountLoad;

      return this.current_user;
    } catch (error) {
      // a failed load must not be served as "the user" by later calls
      this.current_user = undefined;
      throw error;
    } finally {
      this.reload_next_call = false;
    }
  }

  /// Own accounts stay on the get-user payload. Anyone else is loaded from
  /// GET /api/vpn/accounts?target= and replaces the switcher until the
  /// acting user is selected again.
  private followTargetAccounts(emit: boolean): void {
    const username = this.currentTargetUser?.username;

    if (!username || username === this.current_user?.username) {
      this.useOwnAccounts(emit);
      return;
    }

    this.beginForeignAccounts(username, emit);
  }

  private useOwnAccounts(emit: boolean): void {
    this.accountLoadGeneration++;
    this.pendingAccountLoad = undefined;
    this.activeAccounts = undefined;
    this.applyAccountSelection(
      this.current_user?.accounts ?? [],
      this.current_user?.username,
      emit,
    );
  }

  private beginForeignAccounts(username: string, emit: boolean): void {
    const generation = ++this.accountLoadGeneration;
    // Drop the acting user's accounts before the request returns. The header
    // listens here so that list cannot stay under the other user's name.
    this.activeAccounts = [];
    this.currentAccountId = undefined;
    this.accountEventSubject.next(undefined);

    this.pendingAccountLoad = firstValueFrom(
      this.fetchTargetAccounts(username).pipe(catchError(() => of(null))),
    ).then((accounts) => {
      if (generation !== this.accountLoadGeneration) return;

      if (!accounts) {
        this.activeAccounts = [];
        this.currentAccountId = undefined;
        return;
      }

      this.activeAccounts = accounts;
      // A silent reload does not emit from the selection itself. The header
      // still needs this signal once the new rows are in place.
      if (!this.applyAccountSelection(accounts, username, emit)) {
        this.accountEventSubject.next(this.currentAccount);
      }
    });
  }

  /// Returns whether listeners were already notified of the new selection.
  private applyAccountSelection(
    accounts: UserVpnAccount[],
    owner: string | undefined,
    emit: boolean,
  ): boolean {
    const stored = this.readStoredAccount(owner);
    const next = accounts.some((account) => account.id === stored)
      ? stored
      : accounts[0]?.id;
    const changed = this.currentAccountId !== next;
    this.currentAccountId = next;

    if (next !== undefined) {
      this.writeStoredAccount(owner, next);
    }

    if (emit && changed) {
      this.accountEventSubject.next(this.currentAccount);
      return true;
    }

    return false;
  }

  private selectionOwner(): string | undefined {
    return this.viewingOtherUser
      ? this.currentTargetUser?.username
      : this.current_user?.username;
  }

  /// `user|account` stays the acting user's selection. Other panel users
  /// keep theirs in `user|targetAccount`.
  private readStoredAccount(owner: string | undefined): number | undefined {
    if (!owner || owner === this.current_user?.username) {
      const stored = LocalStorageService.get(['user', 'account']);
      return typeof stored === 'number' ? stored : undefined;
    }

    const map = LocalStorageService.get(['user', 'targetAccount']);
    const stored = map?.[owner];
    return typeof stored === 'number' ? stored : undefined;
  }

  private writeStoredAccount(owner: string | undefined, accountId: number): void {
    if (!owner || owner === this.current_user?.username) {
      LocalStorageService.set(['user', 'account'], accountId);
      return;
    }

    const map = LocalStorageService.get(['user', 'targetAccount']);
    const next = map && typeof map === 'object' ? { ...map } : {};
    next[owner] = accountId;
    LocalStorageService.set(['user', 'targetAccount'], next);
  }

  private fetchUser(): Observable<UserModel> {
    const url = `${ACCOUNT_API_URL}/get-user`;
    const title = TranslationService.translate('api.recieve');
    return this.getData<UserModel>(url, undefined, { title });
  }

  private fetchTargetAccounts(username: string): Observable<UserVpnAccount[] | null> {
    return this.getData<UserVpnAccount[]>(`${VPN_API_URL}/accounts`, {
      target: username,
    }).pipe(map((accounts) => (Array.isArray(accounts) ? accounts : null)));
  }
}
