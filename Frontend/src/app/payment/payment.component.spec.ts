import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { PaymentInvoice } from '../@models';
import { TranslationService } from '../@services';
import { API_BASE_URL } from '../@services/api-services/models/app-base-path';
import { PaymentComponent } from './payment.component';

@Component({ template: '' })
class DashboardStubComponent {}

describe('PaymentComponent', () => {
  let http: HttpTestingController;
  let router: Router;
  let translation: TranslationService;

  const pending_invoice: PaymentInvoice = {
    code: 5,
    kind: 2,
    status: 0,
    totalPrice: 5000,
    walletDeduction: 1000,
    payable: 4000,
    walletBalance: 1000,
    allowWallet: true,
    hasReceipt: false,
    items: [
      { title: 'plan', value: 5000 },
      { title: 'wallet', value: -1000 },
    ],
  } as unknown as PaymentInvoice;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([
          { path: 'payment', component: PaymentComponent },
          { path: 'dashboard', component: DashboardStubComponent },
        ]),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    translation = TestBed.inject(TranslationService);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  async function openPayment(query: string): Promise<PaymentComponent> {
    const harness = await RouterTestingHarness.create();
    return await harness.navigateByUrl('/payment' + query, PaymentComponent);
  }

  function flushInvoice(invoice: PaymentInvoice | null) {
    const request = http.expectOne(
      req => req.method === 'GET' && req.url === '/api/billing/get-invoice',
    );
    request.flush({ code: 200, data: invoice }, { status: 200, statusText: 'OK' });
  }

  it('redirects to the dashboard when the invoice code is missing', async () => {
    await openPayment('');
    expect(router.url).toBe('/dashboard');
  });

  it('redirects to the dashboard for a non-numeric invoice code (isNumber fix)', async () => {
    await openPayment('?invoice=abc');
    expect(router.url).toBe('/dashboard');
  });

  it('loads the invoice and renders money amounts in thousands (L1)', async () => {
    const component = await openPayment('?invoice=5');
    flushInvoice(pending_invoice);

    expect(component.invoice?.code).toBe(5);
    expect(component.can_register_receipt).toBeTrue();
    expect(component.can_settle_wallet).toBeTrue();
    expect(component.showBalance(4000)).toBe(
      new Intl.NumberFormat('fa-IR').format(4_000_000),
    );
  });

  it('maps invoice status to its localized title', async () => {
    const component = await openPayment('?invoice=5');
    flushInvoice(pending_invoice);

    expect(component.statusTitle()).toBe(translation.translate('payment.statuses.pending'));
  });

  it('accepts no receipt once the invoice moved to Verifying', async () => {
    const component = await openPayment('?invoice=5');
    flushInvoice({ ...pending_invoice, status: 4 } as PaymentInvoice);
    expect(component.can_register_receipt).toBeFalse();
  });

  it('accepts no receipt when the payable is fully covered by the wallet', async () => {
    const component = await openPayment('?invoice=5');
    flushInvoice({ ...pending_invoice, payable: 0 } as PaymentInvoice);
    expect(component.can_register_receipt).toBeFalse();
  });

  it('settle-wallet posts the code and navigates back to the dashboard', async () => {
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/payment?invoice=5', PaymentComponent);
    flushInvoice(pending_invoice);

    component.settleWallet();
    expect(component.busy).toBeTrue();

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/billing/settle-wallet',
    );
    expect(request.request.body).toEqual({ code: 5 });
    request.flush({ code: 200, data: null }, { status: 200, statusText: 'OK' });
    await harness.fixture.whenStable();

    expect(router.url).toBe('/dashboard');
  });

  it('register-receipt posts exactly one of file or text (XOR)', async () => {
    const component = await openPayment('?invoice=5');
    flushInvoice(pending_invoice);

    // no file picked and empty text: nothing is sent
    component.registerReceipt();
    http.expectNone(
      req => req.method === 'POST' && req.url === '/api/billing/register-receipt',
    );

    // text mode with content
    component.receipt_mode = component.receipt_modes.Text;
    component.receipt_text = '  bank receipt 77  ';
    component.registerReceipt();

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/billing/register-receipt',
    );
    const body = request.request.body as FormData;
    expect(body.get('code')).toBe('5');
    expect(body.get('text')).toBe('bank receipt 77');
    expect(body.get('file')).toBeNull();

    request.flush({ code: 200, data: null }, { status: 200, statusText: 'OK' });

    // registerReceipt reloads the invoice after a successful submission
    flushInvoice(pending_invoice);
  });

  it('after a registered receipt the invoice reloads with the verifying state', async () => {
    const component = await openPayment('?invoice=5');
    flushInvoice(pending_invoice);

    component.receipt_mode = component.receipt_modes.Text;
    component.receipt_text = 'receipt text';
    component.registerReceipt();

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/billing/register-receipt',
    );
    request.flush({ code: 200, data: null }, { status: 200, statusText: 'OK' });

    // registerReceipt reloads the invoice
    flushInvoice({ ...pending_invoice, status: 4, hasReceipt: true } as PaymentInvoice);

    expect(component.invoice?.status).toBe(4);
    expect(component.busy).toBeFalse();
    expect(component.receipt_text).toBe('');
  });

  it('keeps the submit button disabled while a request is busy (M25)', async () => {
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/payment?invoice=5', PaymentComponent);
    flushInvoice(pending_invoice);

    component.settleWallet();
    harness.detectChanges();

    const disabled_button = harness.fixture.nativeElement.querySelector('button[disabled]');
    expect(disabled_button).withContext('settle button disabled while busy').not.toBeNull();

    http.expectOne(
      req => req.method === 'POST' && req.url === '/api/billing/settle-wallet',
    ).flush({ code: 200, data: null }, { status: 200, statusText: 'OK' });

    expect(component.busy).toBeFalse();
  });
});
