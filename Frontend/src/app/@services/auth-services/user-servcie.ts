import { Injectable } from '@angular/core';
import { firstValueFrom, Observable, Subject } from 'rxjs';
import { AccountRole, Target, UserModel } from '../../@models';
import { ApiBaseService } from '../api-services/api-base-service';
import { ACCOUNT_API_URL } from '../api-services/models/app-api-url';
import { LocalStorageService } from '../local-storage/local-storage-service';
import { TranslationService } from '../translation/translation-service';

@Injectable({ providedIn: 'root' })
export class UserService extends ApiBaseService {
  private invoice_code?: string;
  private reload_next_call = false;
  private current_user?: UserModel;
  private observable_user?: Promise<UserModel>;
  private currentTargetUser?: Target;
  private targetUserEventSubject = new Subject<Target | undefined>();

  onTargetChanged = this.targetUserEventSubject.asObservable();

  get targetUser(): Target | undefined {
    return this.currentTargetUser;
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
  /// self financial flows — the server enforces the same matrix (docs/20 §3).
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
      if (!this.reload_next_call && this.observable_user)
        return this.observable_user;

      this.observable_user = firstValueFrom(this.fetchUser());
      this.current_user = await this.observable_user;
      this.observable_user = undefined;

      this.setTraget(LocalStorageService.get(['user', 'target']));
      this.reload_next_call = false;
    }

    return this.current_user;
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

    this.targetUserEventSubject.next(this.currentTargetUser);
  }

  /// On-behalf target chosen from the admin user list (docs/20 phase 6):
  /// the admin's targetArea only holds its own subordinates, while the
  /// server-side access check bypasses every target for the admin role.
  setExternalTarget(target: Target): boolean {
    if (!this.isAdmin || !target?.username) return false;

    if (this.currentTargetUser?.username === target.username) {
      return true;
    }

    this.currentTargetUser = target;
    LocalStorageService.set(['user', 'target'], target.username);

    this.targetUserEventSubject.next(target);

    return true;
  }

  reload() {
    this.reload_next_call = true;
  }

  clear() {
    this.reload_next_call = false;
    this.observable_user = undefined;
    this.current_user = undefined;
    LocalStorageService.set(['user', 'target'], undefined);
  }

  private fetchUser(): Observable<UserModel> {
    const url = `${ACCOUNT_API_URL}/get-user`;
    const title = TranslationService.translate('api.recieve');
    return this.getData<UserModel>(url, undefined, { title });
  }
}
