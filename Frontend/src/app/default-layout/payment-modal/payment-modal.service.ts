import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ShowMessageCase } from '../../@models';
import { ApiBaseService, BILLING_API_URL } from '../../@services';
import { PaymentModalComponent } from './payment-modal.component';

@Injectable({ providedIn: PaymentModalComponent })
export class PaymentModalService extends ApiBaseService {
  paymentRequest(value: number, target?: string): Observable<number | null> {
    return this.postData<number | null>(
      `${BILLING_API_URL}/pay`,
      { value, ...(target ? { target } : {}) },
      { show_message: ShowMessageCase.errors }
    );
  }
}
