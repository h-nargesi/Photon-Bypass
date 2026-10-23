import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router, provideRouter } from '@angular/router';
import { ResultStatus } from '../@models';
import { API_BASE_URL } from '../@services/api-services/models/app-base-path';
import { ForgotPasswordComponent } from './forgot-password.component';

@Component({ template: '' })
class DummyComponent {}

describe('ForgotPasswordComponent', () => {
  let http: HttpTestingController;
  let router: Router;
  let component: ForgotPasswordComponent;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([
          { path: 'forgot-password', component: ForgotPasswordComponent },
          { path: 'login', component: DummyComponent },
        ]),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    component = TestBed.createComponent(ForgotPasswordComponent).componentInstance;
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('blocks the submit until an identifier is entered', () => {
    component.submit();

    expect(component.submitted).toBeTrue();
    http.expectNone(
      req => req.method === 'POST' && req.url === '/api/auth/forget-pass',
    );
  });

  it('sends the address to the reset endpoint and navigates to login', fakeAsync(() => {
    component.getEmailMobile().setValue('user@test.local');

    component.submit();

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/auth/forget-pass',
    );
    expect(request.request.body).toEqual({ emailMobile: 'user@test.local' });

    request.flush(
      { code: 200, message: 'ایمیل بازیابی ارسال شد' },
      { status: 200, statusText: 'OK' },
    );

    expect(component.result?.status()).toBe(ResultStatus.success);

    tick(1000);
    expect(router.url).toBe('/login');
  }));

  it('accepts a plain username as the recovery identifier (D4)', fakeAsync(() => {
    component.getEmailMobile().setValue('tg-user');

    component.submit();

    // no client-side email validation anymore: the username goes through
    // and the server routes the code (telegram chat, email fallback)
    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/auth/forget-pass',
    );
    expect(request.request.body).toEqual({ emailMobile: 'tg-user' });

    request.flush(
      { code: 200, message: 'اگر این حساب ثبت شده باشد، پیام بازیابی ارسال شد.' },
      { status: 200, statusText: 'OK' },
    );

    expect(component.result?.status()).toBe(ResultStatus.success);

    tick(1000);
    expect(router.url).toBe('/login');
  }));

  it('an unknown address shows the server message and stays', fakeAsync(() => {
    component.getEmailMobile().setValue('ghost@test.local');

    component.submit();

    http.expectOne(
      req => req.method === 'POST' && req.url === '/api/auth/forget-pass',
    ).flush(
      { code: 400, message: 'کاربری با این ایمیل یافت نشد!' },
      { status: 200, statusText: 'OK' },
    );

    expect(component.result?.status()).toBe(ResultStatus.error);
    expect(component.result?.message).toBe('کاربری با این ایمیل یافت نشد!');

    tick(1000);
    expect(router.url).not.toBe('/login');
  }));
});
