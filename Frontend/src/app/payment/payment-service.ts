import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PaymentInvoice, ShowMessageCase } from '../@models';
import { ApiBaseService, PLAN_API_URL } from '../@services';
import { PaymentComponent } from './payment.component';

@Injectable({ providedIn: PaymentComponent })
export class PaymentService extends ApiBaseService {
  getInvlice(code: string): Observable<PaymentInvoice> {
    return this.getData<PaymentInvoice>(`${PLAN_API_URL}/get-invoice`, {
      code,
    });
  }

  pay(code: string): Observable<string> {
    return this.postData<string>(
      `${PLAN_API_URL}/pay`,
      { code },
      { show_message: ShowMessageCase.silence }
    );
  }
}
