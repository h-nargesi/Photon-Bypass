import { Injectable } from '@angular/core';
import { firstValueFrom, Observable, Subject } from 'rxjs';
import { Target, UserModel } from '../../@models';
import { ApiBaseService } from '../api-services/api-base-service';
import { AUTH_API_URL } from '../api-services/models/app-api-url';
import { LocalStorageService } from '../local-storage/local-storage-service';
import { TranslationService } from '../translation/translation-service';

@Injectable({ providedIn: 'root' })
export class UserService extends ApiBaseService {
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
    const prv_username = this.currentTargetUser?.username;

    if (!this.current_user?.targetArea) {
      this.currentTargetUser = undefined;
      username = undefined;
    } else {
      if (!username) username = this.current_user.username;

      if (username in this.current_user.targetArea)
        this.currentTargetUser = this.current_user.targetArea[username];
      else throw 'target index is out of range';
    }

    LocalStorageService.set(['user', 'target'], username);

    if (
      this.reload_next_call ||
      (this.current_user?.targetArea &&
        this.currentTargetUser?.username !== prv_username)
    ) {
      this.targetUserEventSubject.next(this.currentTargetUser);
    }
  }

  reload() {
    this.reload_next_call = true;
  }

  clear() {
    this.reload_next_call = false;
    this.observable_user = undefined;
    this.current_user = undefined;
    this.setTraget(undefined);
  }

  private fetchUser(): Observable<UserModel> {
    const url = `${AUTH_API_URL}/get-user`;
    const title = TranslationService.translate('api.recieve');
    return this.getData<UserModel>(url, undefined, { title });
  }
}
