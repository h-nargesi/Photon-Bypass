import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PaymentInvoice, ShowMessageCase } from '../@models';
import { ApiBaseService, BILLING_API_URL } from '../@services';
import { PaymentComponent } from './payment.component';

@Injectable({ providedIn: PaymentComponent })
export class PaymentService extends ApiBaseService {
  getInvoice(code: number, target?: string): Observable<PaymentInvoice | null> {
    return this.getData<PaymentInvoice>(`${BILLING_API_URL}/get-invoice`, {
      code,
      ...(target ? { target } : {}),
    });
  }

  settleWallet(code: number, target?: string): Observable<unknown> {
    return this.postData<unknown>(
      `${BILLING_API_URL}/settle-wallet`,
      { code, ...(target ? { target } : {}) },
      { show_message: ShowMessageCase.success }
    );
  }

  registerReceipt(
    code: number,
    file: File | undefined,
    text: string | undefined,
    target?: string
  ): Observable<unknown> {
    const form = new FormData();
    form.append('code', code.toString());
    if (target) form.append('target', target);
    if (file) form.append('file', file, file.name);
    if (text) form.append('text', text);

    return this.postData<unknown>(`${BILLING_API_URL}/register-receipt`, form, {
      show_message: ShowMessageCase.success,
    });
  }
}
