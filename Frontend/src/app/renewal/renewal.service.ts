import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  RenewalContext,
  PlanInfo,
  PriceModel,
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
import { RenewalComponent } from './renewal.component';

@Injectable({ providedIn: RenewalComponent })
export class RenewalService extends ApiBaseService {
  info(target?: string): Observable<PlanInfo> {
    return this.getData<PlanInfo>(`${PLAN_API_URL}/plan-info`, target ? { target } as ApiParam : undefined);
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
}
