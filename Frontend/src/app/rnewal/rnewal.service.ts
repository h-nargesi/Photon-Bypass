import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  PlanInto,
  PriceModel,
  RnewalResult,
  ShowMessageCase,
} from '../@models';
import {
  ApiBaseService,
  ApiParam,
  BASICS_API_URL,
  PLAN_API_URL,
} from '../@services';
import { RnewalComponent } from './rnewal.component';

@Injectable({ providedIn: RnewalComponent })
export class RnewalService extends ApiBaseService {
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

  rnewal(plan: PlanInto): Observable<RnewalResult> {
    return this.postData<RnewalResult>(`${PLAN_API_URL}/rnewal`, plan);
  }
}
