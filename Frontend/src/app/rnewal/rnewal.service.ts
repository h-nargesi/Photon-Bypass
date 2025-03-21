import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResult, PlanInto } from '../@models';
import { ApiBaseService, PLAN_API_URL } from '../@services';
import { RnewalComponent } from './rnewal.component';

@Injectable({ providedIn: RnewalComponent })
export class RnewalService extends ApiBaseService {
  estimate(plan: PlanInto): Observable<number> {
    return this.postData<number>(`${PLAN_API_URL}/estimate`, plan, undefined);
  }

  rnewal(plan: PlanInto): Observable<ApiResult> {
    return this.job(`${PLAN_API_URL}/rnewal`, plan);
  }
}
