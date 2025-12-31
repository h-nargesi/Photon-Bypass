import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PaymentInvoice, ShowMessageCase } from '../@models';
import { ApiBaseService, PLAN_API_URL } from '../@services';
import { PaymentComponent } from './payment.component';

@Injectable({ providedIn: PaymentComponent })
export class PaymentService extends ApiBaseService {
  getInvlice(code: number): Observable<PaymentInvoice> {
    return this.getData<PaymentInvoice>(`${PLAN_API_URL}/get-invoice`, {
      code,
    });
  }

  pay(value: number): Observable<number> {
    return this.postData<number>(
      `${PLAN_API_URL}/pay`,
      { value },
      { show_message: ShowMessageCase.silence }
    );
  }
}
