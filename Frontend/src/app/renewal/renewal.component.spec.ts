import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { printMoney } from '../@services';
import { API_BASE_URL } from '../@services/api-services/models/app-base-path';
import { RenewalComponent } from './renewal.component';

@Component({ template: '' })
class DummyComponent {}

describe('RenewalComponent', () => {
  let http: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([
          { path: 'renewal', component: RenewalComponent },
          { path: 'payment', component: DummyComponent },
        ]),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  async function openRenewal(): Promise<[RouterTestingHarness, RenewalComponent]> {
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/renewal', RenewalComponent);

    // ngOnInit: load user (blocks the plan-info call behind a microtask), prices load immediately
    const drain = () => new Promise(resolve => setTimeout(resolve, 0));

    http.expectOne('/api/basics/prices').flush(
      { code: 200, data: [] },
      { status: 200, statusText: 'OK' },
    );

    http.expectOne('/api/account/get-user').flush(
      {
        code: 200,
        data: {
          username: 'main',
          fullname: 'Main',
          email: 'main@test.local',
          balance: 0,
          targetArea: { main: { username: 'main', fullname: 'Main', email: 'm@t.l' } },
        },
      },
      { status: 200, statusText: 'OK' },
    );
    await drain();

    http.expectOne('/api/plan/plan-info').flush(
      { code: 200, data: { target: 'main', simultaneousUserCount: 1, days: 120, gigabytes: 50 } },
      { status: 200, statusText: 'OK' },
    );
    await drain();

    // the loaded plan triggers an initial estimate call
    flushEstimate(230, 120);
    await drain();

    return [harness, component];
  }

  function flushEstimate(price: number, days: number) {
    http.expectOne(
      req => req.method === 'POST' && req.url === '/api/plan/estimate',
    ).flush(
      { code: 200, data: { price, days, gigabytes: 50, simultaneousUserCount: 1 } },
      { status: 200, statusText: 'OK' },
    );
  }

  it('loads the last plan into the selectors', async () => {
    const [, component] = await openRenewal();
    // estimate + traffic chart already flushed inside openRenewal

    expect(component.renewal.days).toBe(120);
    expect(component.renewal.gigabytes).toBe(50);
    expect(component.renewal.simultaneousUserCount).toBe(1);
    expect(component.selectedTime).toBe(120);
    expect(component.selectedTraffic).toBe(50);
  });

  it('changing the plan calls estimate and shows the computed cost', async () => {
    const [, component] = await openRenewal();

    component.selectedTraffic = 75;
    component.setTraffic();

    flushEstimate(310, 120);

    expect(component.renewal.gigabytes).toBe(75);
    expect(component.cost).toBe(printMoney(310));
    expect(component.color).toBe('primary');
  });

  it('submit posts the renewal and navigates to payment with the invoice code', async () => {
    const [harness, component] = await openRenewal();

    component.submit();

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/plan/renewal',
    );
    expect(request.request.body).toEqual({
      days: 120,
      gigabytes: 50,
      simultaneousUserCount: 1,
      target: 'main',
    });

    request.flush(
      { code: 200, data: { invoiceCode: 9012, currentPrice: 230 } },
      { status: 200, statusText: 'OK' },
    );
    await new Promise(resolve => setTimeout(resolve, 0));
    await harness.fixture.whenStable();

    expect(router.url).toBe('/payment?invoice=9012');
    expect(component.submitting).toBeFalse();
  });

  it('on-behalf renewal opens the payment page in the payer view (no target)', async () => {
    // manager with a selected dependent target (docs/20 D4: the invoice is
    // issued on the manager, so the payment page must not carry the target)
    localStorage.setItem('user|target', JSON.stringify('sub'));

    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/renewal', RenewalComponent);

    const drain = () => new Promise(resolve => setTimeout(resolve, 0));

    http.expectOne('/api/basics/prices').flush(
      { code: 200, data: [] },
      { status: 200, statusText: 'OK' },
    );

    http.expectOne('/api/account/get-user').flush(
      {
        code: 200,
        data: {
          username: 'main',
          fullname: 'Main',
          email: 'main@test.local',
          balance: 0,
          targetArea: {
            main: { username: 'main', fullname: 'Main', email: 'm@t.l' },
            sub: { username: 'sub', fullname: 'Sub', email: 's@t.l' },
          },
        },
      },
      { status: 200, statusText: 'OK' },
    );
    await drain();

    http.expectOne('/api/plan/plan-info?target=sub').flush(
      { code: 200, data: { target: 'sub', simultaneousUserCount: 1, days: 120, gigabytes: 50 } },
      { status: 200, statusText: 'OK' },
    );
    await drain();

    flushEstimate(230, 120);
    await drain();

    component.submit();

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/plan/renewal',
    );
    expect(request.request.body).toEqual({
      days: 120,
      gigabytes: 50,
      simultaneousUserCount: 1,
      target: 'sub',
    });

    request.flush(
      { code: 200, data: { invoiceCode: 9013, currentPrice: 230 } },
      { status: 200, statusText: 'OK' },
    );
    await new Promise(resolve => setTimeout(resolve, 0));
    await harness.fixture.whenStable();

    expect(router.url).toBe('/payment?invoice=9013');
  });

  it('submit is ignored while a renewal request is in flight (double-submit)', async () => {
    const [, component] = await openRenewal();

    component.submit();
    expect(component.submitting).toBeTrue();

    // second click while the first request is pending must not post again
    component.submit();

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/plan/renewal',
    );
    http.expectNone(
      req => req.method === 'POST' && req.url === '/api/plan/renewal',
    );

    request.flush(
      { code: 200, data: { invoiceCode: 9013, currentPrice: 230 } },
      { status: 200, statusText: 'OK' },
    );
  });

  it('submit without any plan selection does nothing', async () => {
    const [, component] = await openRenewal();

    component.renewal = {} as never;
    component.submit();

    http.expectNone(
      req => req.method === 'POST' && req.url === '/api/plan/renewal',
    );
  });
});
