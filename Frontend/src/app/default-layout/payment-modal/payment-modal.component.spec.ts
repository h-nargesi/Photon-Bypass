import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router, provideRouter } from '@angular/router';
import { UserService } from '../../@services';
import { API_BASE_URL } from '../../@services/api-services/models/app-base-path';
import { PaymentModalComponent } from './payment-modal.component';

@Component({ template: '' })
class PaymentStubComponent {}

describe('PaymentModalComponent', () => {
  let http: HttpTestingController;
  let router: Router;
  let fixture: ComponentFixture<PaymentModalComponent>;
  let component: PaymentModalComponent;
  let user_service: UserService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([{ path: 'payment', component: PaymentStubComponent }]),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    user_service = TestBed.inject(UserService);
    fixture = TestBed.createComponent(PaymentModalComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('rejects amounts at or below the minimum without any request', () => {
    component.value = 10000;

    component.submit();

    http.expectNone(
      req => req.method === 'POST' && req.url === '/api/billing/pay',
    );

    component.value = 5000;
    component.submit();

    http.expectNone(
      req => req.method === 'POST' && req.url === '/api/billing/pay',
    );
  });

  it('posts the amount to /billing/pay and navigates with the returned invoice code', async () => {
    component.value = 20000;

    component.submit();
    expect(component.busy).toBeTrue();

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/billing/pay',
    );
    expect(request.request.body).toEqual({ value: 20000 });

    request.flush({ code: 200, data: 4451 }, { status: 200, statusText: 'OK' });
    await fixture.whenStable();

    expect(router.url).toBe('/payment?invoice=4451');
    expect(user_service.invoice).toBe('4451');
    expect(component.busy).toBeFalse();
  });

  it('a null result keeps the modal open and clears busy', async () => {
    component.value = 20000;

    component.submit();

    http.expectOne(
      req => req.method === 'POST' && req.url === '/api/billing/pay',
    ).flush({ code: 400, message: 'invalid' }, { status: 200, statusText: 'OK' });
    await fixture.whenStable();

    expect(component.busy).toBeFalse();
    expect(router.url).not.toContain('/payment');
  });
});
