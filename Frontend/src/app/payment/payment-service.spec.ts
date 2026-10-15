import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { PaymentInvoice } from '../@models';
import { API_BASE_URL } from '../@services/api-services/models/app-base-path';
import { PaymentService } from './payment-service';

describe('PaymentService', () => {
  let service: PaymentService;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    service = TestBed.inject(PaymentService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('getInvoice loads /billing/get-invoice by code and unwraps the data', async () => {
    const invoice: PaymentInvoice = {
      code: 7,
      kind: 2,
      status: 0,
      totalPrice: 1000,
      walletDeduction: 0,
      payable: 1000,
      hasReceipt: false,
    } as unknown as PaymentInvoice;

    const result_promise = firstValueFrom(service.getInvoice(7));
    const request = http.expectOne(
      req => req.method === 'GET' && req.url === '/api/billing/get-invoice',
    );
    expect(request.request.params.get('code')).toBe('7');
    request.flush({ code: 200, data: invoice }, { status: 200, statusText: 'OK' });

    const result = await result_promise;
    expect(result?.code).toBe(7);
    expect(result?.payable).toBe(1000);
  });

  it('getInvoice appends the target query parameter when provided', async () => {
    const result_promise = firstValueFrom(service.getInvoice(7, 'sub1'));
    const request = http.expectOne(
      req => req.method === 'GET' && req.url === '/api/billing/get-invoice',
    );
    expect(request.request.params.get('target')).toBe('sub1');
    request.flush({ code: 200, data: null }, { status: 200, statusText: 'OK' });

    expect(await result_promise).toBeNull();
  });

  it('settleWallet posts the code to /billing/settle-wallet', async () => {
    const result_promise = firstValueFrom(service.settleWallet(9));
    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/billing/settle-wallet',
    );
    expect(request.request.body).toEqual({ code: 9 });
    request.flush({ code: 200, data: 'ok' }, { status: 200, statusText: 'OK' });

    expect(await result_promise).toBe('ok');
  });

  it('registerReceipt posts a multipart form with code, file and text', async () => {
    const file = new File(['image-bytes'], 'receipt.jpg', { type: 'image/jpeg' });

    const result_promise = firstValueFrom(
      service.registerReceipt(5, file, 'bank receipt 123'),
    );
    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/billing/register-receipt',
    );

    const body = request.request.body;
    expect(body).toBeInstanceOf(FormData);
    expect((body as FormData).get('code')).toBe('5');
    expect(((body as FormData).get('file') as File).name).toBe('receipt.jpg');
    expect((body as FormData).get('text')).toBe('bank receipt 123');

    request.flush({ code: 200, data: null }, { status: 200, statusText: 'OK' });
    await result_promise;
  });

  it('registerReceipt sends text-only forms without a file part', async () => {
    const result_promise = firstValueFrom(service.registerReceipt(5, undefined, 'text only'));
    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/billing/register-receipt',
    );

    const body = request.request.body as FormData;
    expect(body.get('file')).toBeNull();
    expect(body.get('text')).toBe('text only');

    request.flush({ code: 200, data: null }, { status: 200, statusText: 'OK' });
    await result_promise;
  });
});
