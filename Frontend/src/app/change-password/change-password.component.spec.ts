import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { API_BASE_URL } from '../@services/api-services/models/app-base-path';
import { ChangePasswordComponent } from './change-password.component';

@Component({ template: '' })
class DummyComponent {}

describe('ChangePasswordComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([
          { path: 'change-password', component: ChangePasswordComponent },
          { path: 'change-ovpn-password', component: ChangePasswordComponent },
          { path: 'reset-password', component: ChangePasswordComponent },
          { path: 'login', component: DummyComponent },
          { path: 'dashboard', component: DummyComponent },
        ]),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  function fillForm(component: ChangePasswordComponent, password = 'Passw0rd1') {
    component.form.controls['password'].setValue(password);
    component.form.controls['confirmPassword'].setValue(password);
    component.form.controls['token']?.setValue('reset-token');
  }

  it('rejects a password without digits and mixed case', async () => {
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/change-password', ChangePasswordComponent);

    fillForm(component, 'weakpass');
    component.submit();

    expect(component.submitted).toBeTrue();
    http.expectNone(
      req => req.method === 'POST' && req.url === '/api/account/change-pass',
    );
  });

  it('rejects mismatching confirmation', async () => {
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/change-password', ChangePasswordComponent);

    fillForm(component);
    component.form.controls['confirmPassword'].setValue('Different1');
    component.submit();

    expect(component.form.errors).toEqual({ passwordMismatch: true });
    http.expectNone(
      req => req.method === 'POST' && req.url === '/api/account/change-pass',
    );
  });

  it('posts token and password to the change-pass endpoint', fakeAsync(async () => {
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/change-password', ChangePasswordComponent);

    fillForm(component);
    component.submit();

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/account/change-pass',
    );
    expect(request.request.body['token']).toBe('reset-token');
    expect(request.request.body['password']).toBe('Passw0rd1');

    request.flush({ code: 200, message: 'ok' }, { status: 200, statusText: 'OK' });
    tick(1000);
  }));

  it('reset mode seeds the token from the query string', async () => {
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl(
      '/reset-password?token=abc123',
      ChangePasswordComponent,
    );

    expect(component.form.controls['token']).toBeUndefined();
    expect(component.model.token).toBe('abc123');

    fillForm(component);
    component.submit();

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/account/change-pass',
    );
    expect(request.request.body['token']).toBe('abc123');

    request.flush({ code: 200, message: 'ok' }, { status: 200, statusText: 'OK' });
  });
});
