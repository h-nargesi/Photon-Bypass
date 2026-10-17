import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AccountRole,
  ApiResult,
  CreateUserModel,
  PagedResult,
  UserRow,
} from '../../@models';
import { ApiBaseService, ApiParam, USER_API_URL } from '../../@services';

/// Manager/admin user management API (docs/20 phase 4).
@Injectable({ providedIn: 'root' })
export class UsersService extends ApiBaseService {
  list(search?: string, page: number = 1): Observable<PagedResult<UserRow>> {
    // build conditionally — null params would serialize as the literal "null"
    const params: ApiParam = { page };
    if (search) params['search'] = search;
    return this.getData<PagedResult<UserRow>>(
      `${USER_API_URL}/list`,
      params,
    );
  }

  create(model: CreateUserModel): Observable<ApiResult> {
    return this.job(`${USER_API_URL}/create`, model);
  }

  setPassword(target: string, password: string): Observable<ApiResult> {
    return this.job(`${USER_API_URL}/set-password`, { target, password });
  }

  setRole(target: string, role: AccountRole): Observable<ApiResult> {
    return this.job(`${USER_API_URL}/set-role`, { target, role });
  }

  setOwner(target: string, owner: string | null): Observable<ApiResult> {
    return this.job(`${USER_API_URL}/set-owner`, { target, owner });
  }

  setSelfService(target: string, allow: boolean): Observable<ApiResult> {
    return this.job(`${USER_API_URL}/set-self-service`, { target, allow });
  }

  setActive(target: string, active: boolean): Observable<ApiResult> {
    return this.job(`${USER_API_URL}/set-active`, { target, active });
  }
}
