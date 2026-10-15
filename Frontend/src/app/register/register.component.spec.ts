import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
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
});
