import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  PlanInto,
  PriceModel,
  RenewalResult,
  ShowMessageCase,
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
  info(target?: string): Observable<PlanInto> {
    return this.getData<PlanInto>(`${PLAN_API_URL}/plan-info`, {
      target,
    } as ApiParam);
  }

  prices(): Observable<PriceModel[]> {
    return this.getData<PriceModel[]>(`${BASICS_API_URL}/prices`);
  }

  estimate(plan: PlanInto): Observable<number> {
    return this.postData<number>(`${PLAN_API_URL}/estimate`, plan, {
      show_message: ShowMessageCase.errors,
    });
  }

  renewal(plan: PlanInto): Observable<RenewalResult> {
    return this.postData<RenewalResult>(`${PLAN_API_URL}/renewal`, plan);
  }
}
