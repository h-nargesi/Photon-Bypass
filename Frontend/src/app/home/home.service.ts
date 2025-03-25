import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PriceModel } from '../@models';
import { ApiBaseService, BASICS_API_URL } from '../@services';
import { HomeComponent } from './home.component';

@Injectable({ providedIn: HomeComponent })
export class HomeService extends ApiBaseService {
  prices(): Observable<PriceModel[]> {
    return this.getData<PriceModel[]>(`${BASICS_API_URL}/prices`);
  }
}
