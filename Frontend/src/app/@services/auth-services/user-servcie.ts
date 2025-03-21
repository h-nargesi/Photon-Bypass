import { Injectable } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import { UserModel } from '../../@models';
import { ApiBaseService } from '../api-services/api-base-service';
import { AUTH_API_URL } from '../api-services/models/app-api-url';
import { TranslationService } from '../translation/translation-service';
import { LocalStorageService } from '../local-storage/local-storage-service';

@Injectable({ providedIn: 'root' })
export class UserService extends ApiBaseService {
  private current_user?: UserModel;
  private observable_user?: Promise<UserModel>;
  private targetUser?: string;

  public get Target(): string | undefined {
    return this.targetUser;
  }

  public async user(): Promise<UserModel> {
    if (this.current_user === undefined) {
      if (this.observable_user) return this.observable_user;

      this.observable_user = firstValueFrom(this.fetchUser());
      this.current_user = await this.observable_user;
      this.observable_user = undefined;

      const targetIndex: number =
        LocalStorageService.get(['user', 'target']) ?? -1;
      // reset target index in error cases
      LocalStorageService.set(['user', 'target'], -1);
      this.setTraget(targetIndex);
    }

    return this.current_user;
  }

  public setTraget(index: number) {
    if (!this.current_user?.targetArea) this.targetUser = undefined;
    else {
      if (index === -1) this.targetUser = this.current_user.username;
      else if (index < 0 || this.current_user.targetArea.length <= index)
        throw 'target index is out of range';
      else this.targetUser = this.current_user.targetArea[index];
      LocalStorageService.set(['user', 'target'], index);
    }
  }

  public clear() {
    this.current_user = undefined;
    this.targetUser = undefined;
  }

  private fetchUser(): Observable<UserModel> {
    const url = `${AUTH_API_URL}/get-user`;
    const title = TranslationService.translate('api.recieve');
    return this.getData<UserModel>(url, undefined, { title });
  }
}
