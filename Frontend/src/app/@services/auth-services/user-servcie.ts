import { Injectable } from '@angular/core';
import { firstValueFrom, Observable, Subject } from 'rxjs';
import { UserModel } from '../../@models';
import { ApiBaseService } from '../api-services/api-base-service';
import { AUTH_API_URL } from '../api-services/models/app-api-url';
import { LocalStorageService } from '../local-storage/local-storage-service';
import { TranslationService } from '../translation/translation-service';

@Injectable({ providedIn: 'root' })
export class UserService extends ApiBaseService {
  private current_user?: UserModel;
  private observable_user?: Promise<UserModel>;
  private targetUser: string = '';
  private targetUserEventSubject = new Subject<string>();
  public onTargetChanged = this.targetUserEventSubject.asObservable();

  public get Target(): string {
    return this.targetUser;
  }

  public async user(): Promise<UserModel> {
    if (this.current_user === undefined) {
      if (this.observable_user) return this.observable_user;

      this.observable_user = firstValueFrom(this.fetchUser());
      this.current_user = await this.observable_user;
      this.observable_user = undefined;

      const target: string = LocalStorageService.get(['user', 'target']);
      let targetIndex = -1;
      if (target && this.current_user?.targetArea) {
        targetIndex = this.current_user.targetArea.indexOf(target);
      }
      this.setTraget(targetIndex);
    }

    return this.current_user;
  }

  public setTraget(index: number) {
    const current_state = this.targetUser;

    if (!this.current_user?.targetArea) this.targetUser = '';
    else {
      if (index === -1) this.targetUser = this.current_user.username;
      else if (index < 0 || this.current_user.targetArea.length <= index)
        throw 'target index is out of range';
      else this.targetUser = this.current_user.targetArea[index];
    }

    if (this.targetUser && this.targetUser !== current_state)
      this.targetUserEventSubject.next(this.targetUser);

    LocalStorageService.set(['user', 'target'], this.targetUser);
  }

  public clear() {
    this.current_user = undefined;
    this.setTraget(-1);
  }

  private fetchUser(): Observable<UserModel> {
    const url = `${AUTH_API_URL}/get-user`;
    const title = TranslationService.translate('api.recieve');
    return this.getData<UserModel>(url, undefined, { title });
  }
}
