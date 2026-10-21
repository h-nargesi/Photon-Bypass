import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute } from '@angular/router';
import { Router, provideRouter } from '@angular/router';
import { ResultStatus } from '../@models';
import { API_BASE_URL } from '../@services/api-services/models/app-base-path';
import { RegisterComponent } from './register.component';

@Component({ template: '' })
class DummyComponent {}

describe('RegisterComponent', () => {
  let http: HttpTestingController;
  let router: Router;
  let component: RegisterComponent;
  let fixture: ComponentFixture<RegisterComponent>;

  function flushEditModeBootstrap(telegram: { connected: boolean }) {
    // request order: get-user (target resolution) and the telegram status
    // fire synchronously; full-info only after the awaited user() resumes
    http
      .expectOne(req => req.url === '/api/account/get-user')
      .flush(
        {
          code: 200,
          data: {
            username: 'tester',
            fullname: 'Test User',
            email: 'tester@test.local',
            balance: 0,
            role: 0,
            allowSelfService: true,
            targetArea: {
              tester: { username: 'tester', fullname: 'Test User', email: 'tester@test.local' },
            },
          },
        },
        { status: 200, statusText: 'OK' },
      );

    http
      .expectOne(req => req.url === '/api/account/telegram-link')
      .flush({ code: 200, data: telegram }, { status: 200, statusText: 'OK' });

    tick();

    http
      .expectOne(req => req.url === '/api/account/full-info')
      .flush(
        {
          code: 200,
          data: {
            username: 'tester',
            email: 'tester@test.local',
            emailValid: true,
            mobile: '',
            mobileValid: false,
            firstname: 'Test',
            lastname: 'User',
          },
        },
        { status: 200, statusText: 'OK' },
      );

    tick();
  }

  describe('register mode', () => {
    beforeEach(() => {
      localStorage.clear();
      TestBed.configureTestingModule({
        providers: [
          provideHttpClient(),
          provideHttpClientTesting(),
          provideNoopAnimations(),
          provideRouter([
            { path: 'login', component: DummyComponent },
            { path: 'register', component: RegisterComponent },
          ]),
          { provide: API_BASE_URL, useValue: '/api' },
        ],
      });
      http = TestBed.inject(HttpTestingController);
      router = TestBed.inject(Router);
      fixture = TestBed.createComponent(RegisterComponent);
      component = fixture.componentInstance;
    });

    afterEach(() => {
      // register mode fetches the public app-config (D11) on init — answer any
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

    function fillValidForm() {
      component.form.controls['username'].setValue('tester');
      component.form.controls['email'].setValue('tester@test.local');
      component.form.controls['mobile'].setValue('+989120000000');
      component.form.controls['firstname'].setValue('Test');
      component.form.controls['lastname'].setValue('User');
      component.form.controls['password'].setValue('Passw0rd1');
      component.form.controls['confirmPassword'].setValue('Passw0rd1');
    }

    it('creates in register mode with validation thresholds', () => {
      expect(component.isEdit).toBeFalse();
      expect(component.ValidatorValues.password.minLength).toBe(8);
      expect(component.ValidatorValues.username.minLength).toBe(5);
    });

    it('rejects a short password before any request', () => {
      fillValidForm();
      component.form.controls['password'].setValue('short');
      component.form.controls['confirmPassword'].setValue('short');

      component.submit();

      expect(component.submitted).toBeTrue();
      http.expectNone(
        req => req.method === 'POST' && req.url === '/api/auth/register',
      );
    });

    it('rejects a username with invalid characters', () => {
      fillValidForm();
      component.form.controls['username'].setValue('1abc');

      component.submit();

      expect(component.form.invalid).toBeTrue();
      http.expectNone(
        req => req.method === 'POST' && req.url === '/api/auth/register',
      );
    });

    it('rejects mismatching confirm password', () => {
      fillValidForm();
      component.form.controls['confirmPassword'].setValue('Different1');

      component.submit();

      expect(component.form.errors).toEqual({ passwordMismatch: true });
      http.expectNone(
        req => req.method === 'POST' && req.url === '/api/auth/register',
      );
    });

    it('submits the form to the register endpoint', fakeAsync(() => {
      fillValidForm();

      component.submit();

      const request = http.expectOne(
        req => req.method === 'POST' && req.url === '/api/auth/register',
      );
      expect(request.request.body['username']).toBe('tester');
      expect(request.request.body['email']).toBe('tester@test.local');

      request.flush(
        { code: 200, message: 'ثبت نام با موفقیت انجام شد' },
        { status: 200, statusText: 'OK' },
      );

      expect(component.result?.status()).toBe(ResultStatus.success);

      tick(1000);
      expect(router.url).toBe('/login');
    }));

    it('keeps the button disabled while the registration is in flight (double-submit)', fakeAsync(() => {
      fixture.detectChanges(); // ngOnInit -> app-config lookup
      http.expectOne(req => req.url === '/api/basics/app-config').flush(
        { code: 200, data: { allowRegistration: true } },
        { status: 200, statusText: 'OK' },
      );
      fixture.detectChanges(); // the form is rendered

      fillValidForm();

      component.submit();
      fixture.detectChanges();

      expect(component.submitted).toBeTrue();
      expect(fixture.nativeElement.querySelector('button[disabled]')).not.toBeNull();

      // the disabled button is the only guard: no second request can be triggered
      // through the UI while the first registration is pending
      http.expectOne(
        req => req.method === 'POST' && req.url === '/api/auth/register',
      ).flush(
        { code: 400, message: 'duplicate' },
        { status: 200, statusText: 'OK' },
      );

      expect(component.submitted).toBeFalse();
    }));

    it('a failed registration keeps the user on the form with the message', fakeAsync(() => {
      fillValidForm();

      component.submit();

      http.expectOne(
        req => req.method === 'POST' && req.url === '/api/auth/register',
      ).flush(
        { code: 400, message: 'این نام کاربری قبلا استفاده شده است!' },
        { status: 200, statusText: 'OK' },
      );

      expect(component.result?.status()).toBe(ResultStatus.error);
      expect(component.submitted).toBeFalse();

      tick(1000);
      expect(router.url).not.toBe('/login');
    }));

    it('shows the closed notice instead of the form when registration is off (D11)', () => {
      fixture.detectChanges();

      http.expectOne(req => req.url === '/api/basics/app-config').flush(
        { code: 200, data: { allowRegistration: false } },
        { status: 200, statusText: 'OK' },
      );
      fixture.detectChanges();

      expect(component.allow_registration).toBeFalse();

      const element: HTMLElement = fixture.nativeElement;
      expect(element.querySelector('form')).toBeNull();
      expect(element.textContent).toContain('ثبت نام در حال حاضر فعال نیست');

      http.expectNone(
        req => req.method === 'POST' && req.url === '/api/auth/register',
      );
    });
  });

  describe('edit mode (telegram connection card)', () => {
    beforeEach(() => {
      localStorage.clear();
      TestBed.configureTestingModule({
        providers: [
          provideHttpClient(),
          provideHttpClientTesting(),
          provideNoopAnimations(),
          provideRouter([{ path: 'login', component: DummyComponent }]),
          // after provideRouter so the stub wins over the router's route:
          // this describe instantiates the component in edit-user-info mode
          { provide: API_BASE_URL, useValue: '/api' },
          {
            provide: ActivatedRoute,
            useValue: { routeConfig: { path: 'edit-user-info' } },
          },
        ],
      });
      http = TestBed.inject(HttpTestingController);
      router = TestBed.inject(Router);
      fixture = TestBed.createComponent(RegisterComponent);
      component = fixture.componentInstance;
    });

    afterEach(() => {
      http.verify();
      localStorage.clear();
    });

    it('loads the telegram connection status with the form', fakeAsync(() => {
      fixture.detectChanges();
      flushEditModeBootstrap({ connected: false });
      tick();
      fixture.detectChanges();

      expect(component.isEdit).toBeTrue();
      expect(component.telegram_connected).toBeFalse();

      const element: HTMLElement = fixture.nativeElement;
      expect(element.textContent).toContain('دریافت کد اتصال');
      // no deep link before a code is issued
      expect(element.querySelector('a[href^="https://t.me/"]')).toBeNull();
    }));

    it('issuing a code shows the deep link and the one-time code', fakeAsync(() => {
      fixture.detectChanges();
      flushEditModeBootstrap({ connected: false });
      tick();
      fixture.detectChanges();

      component.createTelegramLink();

      http
        .expectOne(
          req =>
            req.method === 'POST' &&
            // the target rides inside the URL string, not the params option
            req.url.split('?')[0] === '/api/account/telegram-link',
        )
        .flush(
          {
            code: 200,
            data: {
              connected: false,
              code: 'the-one-time-code',
              url: 'https://t.me/TestBot?start=the-one-time-code',
            },
          },
          { status: 200, statusText: 'OK' },
        );
      tick();
      fixture.detectChanges();

      const element: HTMLElement = fixture.nativeElement;
      const deep_link = element.querySelector<HTMLAnchorElement>('a[href^="https://t.me/"]');

      expect(deep_link).not.toBeNull();
      expect(deep_link!.href).toBe('https://t.me/TestBot?start=the-one-time-code');
      expect(
        (element.querySelector('input.telegram-code-input') as HTMLInputElement)
          ?.value,
      ).toBe('the-one-time-code');
    }));

    it('a connected account offers the disconnect button and detaches on click', fakeAsync(() => {
      fixture.detectChanges();
      flushEditModeBootstrap({ connected: true });
      tick();
      fixture.detectChanges();

      expect(component.telegram_connected).toBeTrue();

      const element: HTMLElement = fixture.nativeElement;
      expect(element.textContent).toContain('قطع اتصال تلگرام');

      component.disconnectTelegram();

      http
        .expectOne(
          req =>
            req.method === 'DELETE' &&
            req.url.split('?')[0] === '/api/account/telegram-link',
        )
        .flush(
          { code: 200, message: 'اتصال تلگرام قطع شد.' },
          { status: 200, statusText: 'OK' },
        );
      tick();
      fixture.detectChanges();

      expect(component.telegram_connected).toBeFalse();
      expect(component.telegram_link).toBeUndefined();
      expect(element.textContent).toContain('دریافت کد اتصال');
    }));
  });
});
