import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ShowMessageCase } from '../../@models';
import { ApiBaseService, PLAN_API_URL } from '../../@services';
import { PaymentModalComponent } from './payment-modal.component';

@Injectable({ providedIn: PaymentModalComponent })
export class PaymentModalService extends ApiBaseService {
  paymentRequest(value: number): Observable<string> {
    return this.postData<string>(
      `${PLAN_API_URL}/payment-request`,
      { value },
      { show_message: ShowMessageCase.silence }
    );
  }
}
