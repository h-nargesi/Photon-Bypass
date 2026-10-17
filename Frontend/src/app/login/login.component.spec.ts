import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router, provideRouter } from '@angular/router';
import { ResultStatus } from '../@models';
import { API_BASE_URL } from '../@services/api-services/models/app-base-path';
import { LoginComponent } from './login.component';

@Component({ template: '' })
class DummyComponent {}

describe('LoginComponent', () => {
  let http: HttpTestingController;
  let router: Router;
  let fixture: ComponentFixture<LoginComponent>;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([
          { path: 'login', component: DummyComponent },
          { path: 'dashboard', component: DummyComponent },
        ]),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(LoginComponent);

    // constructor auth-check: no active session
    http.expectOne('/api/account/get-user').flush(
      { code: 401 },
      { status: 200, statusText: 'OK' },
    );
  });

  afterEach(() => {
    // the component fetches the public app-config (D11) on init — answer any
    // pending lookup so verify() stays strict about real leaks
    http
      .match(req => req.url === '/api/basics/app-config')
      .forEach(request =>
        request.flush(
          { code: 200, data: { allowRegistration: true } },
          { status: 200, statusText: 'OK' },
        ),
      );
    http.verify();
    localStorage.clear();
  });

  it('creates with the default dashboard redirect', () => {
    const component = fixture.componentInstance;

    expect(component).toBeTruthy();
    expect(component.redirect).toBe('/dashboard');
  });

  it('does not call the token endpoint while fields are empty', () => {
    const component = fixture.componentInstance;

    component.login();

    expect(component.isValidated).toBeTrue();
    expect(component.busy).toBeFalse();
    http.expectNone(
      req => req.method === 'POST' && req.url === '/api/auth/token',
    );
  });

  it('keeps the button disabled while a login request is busy', () => {
    const component = fixture.componentInstance;
    component.username = 'user';
    component.password = 'Passw0rd';

    component.login();
    fixture.detectChanges();

    expect(component.busy).toBeTrue();
    expect(fixture.nativeElement.querySelector('button[disabled]')).not.toBeNull();

    http.expectOne(
      req => req.method === 'POST' && req.url === '/api/auth/token',
    ).flush({ code: 200, data: 'token' }, { status: 200, statusText: 'OK' });

    expect(component.busy).toBeFalse();
  });

  it('failed login surfaces the message and stays on the page', () => {
    const component = fixture.componentInstance;
    component.username = 'user';
    component.password = 'Passw0rd';

    component.login();

    http.expectOne(
      req => req.method === 'POST' && req.url === '/api/auth/token',
    ).flush(
      { code: 400, message: 'نام کاربری یا رمز عبور اشتباه است!' },
      { status: 200, statusText: 'OK' },
    );

    expect(component.result?.status()).toBe(ResultStatus.error);
    expect(router.url).not.toBe('/dashboard');
  });

  it('successful login stores the token and navigates to the redirect', fakeAsync(() => {
    const component = fixture.componentInstance;
    component.username = 'user';
    component.password = 'Passw0rd';

    component.login();

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/auth/token',
    );
    expect(request.request.body).toEqual({ username: 'user', password: 'Passw0rd' });

    request.flush(
      { code: 200, data: 'jwt-token' },
      { status: 200, statusText: 'OK' },
    );

    expect(JSON.parse(localStorage.getItem('user|bearer')!)).toBe('jwt-token');
    expect(component.result?.status()).toBe(ResultStatus.success);

    tick(1000);
    expect(router.url).toBe('/dashboard');
  }));

  it('hides the register CTA while registration is disabled (D11)', () => {
    const component = fixture.componentInstance;
    fixture.detectChanges();

    http.expectOne(req => req.url === '/api/basics/app-config').flush(
      { code: 200, data: { allowRegistration: false } },
      { status: 200, statusText: 'OK' },
    );
    fixture.detectChanges();

    expect(component.allow_registration).toBeFalse();
    expect(fixture.nativeElement.querySelector('[routerLink="/register"]')).toBeNull();
  });
});
