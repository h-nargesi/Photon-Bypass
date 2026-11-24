import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResultData, PriceModel, ShowMessageCase } from '../@models';
import { ApiBaseService, BASICS_API_URL } from '../@services';

@Injectable({ providedIn: 'root' })
export class HomeService extends ApiBaseService {
  /// The home page is a top-level route without a toaster: it renders the answer
  /// (including a failed load) itself, so nothing is raised as a toast.
  prices(): Observable<ApiResultData<PriceModel[]>> {
    return this.get<PriceModel[]>(`${BASICS_API_URL}/prices`, undefined, {
      show_message: ShowMessageCase.silence,
    });
  }
}
