import { Injectable } from '@angular/core';
import { Observable, of, tap } from 'rxjs';
import { UserModel } from '../../@models';
import { ApiBaseService } from '../api-services/api-base-service';
import {
  LocalStorageService,
  wait,
} from '../local-storage/local-storage-service';
import { TranslationService } from '../translation/translation-service';

@Injectable({ providedIn: 'root' })
export class UserService extends ApiBaseService {
  private current_user?: UserModel;
  private observable_user?: Observable<UserModel>;

  public user(): Observable<UserModel> {
    if (this.current_user || this.current_user === null) {
      return of(this.current_user);
    } else if (!this.observable_user) {
      this.observable_user = this.fetchUser();
      this.observable_user.pipe(
        tap((user) => {
          this.current_user = user;
          this.observable_user = undefined;
        })
      );
    }

    return this.observable_user;
  }

  public clear() {
    this.current_user = undefined;
  }

  private fetchUser(): Observable<UserModel> {
    if (LocalStorageService.UseAPI) {
      const url = `${ACCOUNT_API_URL}/get-user`;
      const title = TranslationService.translate('api.recieve');
      return this.getData<UserModel>(url, undefined, title);
    } else {
      const bearer = LocalStorageService.get(['user', 'bearer']);
      console.log('user-check-bearer', bearer);
      let current_user: UserModel | null;
      if (bearer) {
        current_user = {
          username: 'hamed@na',
          fullname: 'حامد نرگسی',
          email: 'hamed.nargesi.jar@gmail.com',
        };
      } else {
        current_user = null;
      }
      return wait(current_user as UserModel);
    }
  }
}

const ACCOUNT_API_URL: string = '/account';
