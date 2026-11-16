import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  RenewalContext,
  NewAccountContext,
  PlanInfo,
  PriceModel,
  RealmChoice,
  RenewalResult,
  ShowMessageCase,
  EstimateResult,
} from '../@models';
import {
  ApiBaseService,
  ApiParam,
  BASICS_API_URL,
  PLAN_API_URL,
} from '../@services';

@Injectable({ providedIn: 'root' })
export class RenewalService extends ApiBaseService {
  info(accountId?: number): Observable<PlanInfo> {
    return this.getData<PlanInfo>(`${PLAN_API_URL}/plan-info`, accountId ? { accountId } as ApiParam : undefined);
  }

  realms(): Observable<RealmChoice[]> {
    return this.getData<RealmChoice[]>(`${PLAN_API_URL}/realms`);
  }

  suggestUsername(): Observable<string> {
    return this.getData<string>(`${PLAN_API_URL}/suggest-username`);
  }

  prices(): Observable<PriceModel[]> {
    return this.getData<PriceModel[]>(`${BASICS_API_URL}/prices`);
  }

  estimate(plan: RenewalContext): Observable<EstimateResult> {
    return this.postData<EstimateResult>(`${PLAN_API_URL}/estimate`, plan, {
      show_message: ShowMessageCase.errors,
    });
  }

  renewal(plan: RenewalContext): Observable<RenewalResult> {
    return this.postData<RenewalResult>(`${PLAN_API_URL}/renewal`, plan);
  }

  newAccount(context: NewAccountContext): Observable<RenewalResult> {
    return this.postData<RenewalResult>(`${PLAN_API_URL}/new-account`, context);
  }
}
