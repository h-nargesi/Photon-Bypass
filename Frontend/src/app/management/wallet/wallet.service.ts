import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ApiResult,
  BalanceDirection,
  PagedResult,
  WalletTransaction,
} from '../../@models';
import { ApiBaseService, ApiParam, WALLET_API_URL } from '../../@services';

/// Admin wallet adjust + history API (docs/20 phase 3, D5).
@Injectable({ providedIn: 'root' })
export class WalletService extends ApiBaseService {
  adjust(
    target: string,
    direction: BalanceDirection,
    amount: number,
    description: string,
  ): Observable<ApiResult> {
    return this.job(`${WALLET_API_URL}/adjust`, {
      target,
      direction,
      amount,
      description,
    });
  }

  history(
    target?: string,
    page: number = 1,
  ): Observable<PagedResult<WalletTransaction>> {
    // build conditionally — null params would serialize as the literal "null"
    const params: ApiParam = { page };
    if (target) params['target'] = target;
    return this.getData<PagedResult<WalletTransaction>>(
      `${WALLET_API_URL}/history`,
      params,
    );
  }
}
