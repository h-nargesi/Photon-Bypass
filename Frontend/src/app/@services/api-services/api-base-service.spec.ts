import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthService } from '../auth-services/auth-service';
import { UserService } from '../auth-services/user-servcie';
import { LocalStorageService } from '../local-storage/local-storage-service';
import { ApiBaseService } from './api-base-service';
import { API_BASE_URL } from './models/app-base-path';

@Component({ template: '' })
class DummyComponent {}

class RedirectLatch extends ApiBaseService {
  static arm(): void {
    ApiBaseService.is_logout = false;
  }
}

describe('ApiBaseService (401 redirect, UserService as the concrete subclass)', () => {
  let http: HttpTestingController;
  let router: Router;
  let users: UserService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'login', component: DummyComponent },
          { path: 'dashboard', component: DummyComponent },
        ]),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    users = TestBed.inject(UserService);
    spyOn(router, 'navigateByUrl');
    RedirectLatch.arm();
  });

  afterEach(() => {
    RedirectLatch.arm();
    http.verify();
    localStorage.clear();
  });

  async function rejectedLoad(): Promise<void> {
    const promise = users.user().catch(() => undefined);
    http.expectOne('/api/account/get-user').flush(
      { code: 401 },
      { status: 401, statusText: 'Unauthorized' },
    );
    await promise;
  }

  it('a 401 clears the bearer and redirects to /login with the return url', async () => {
    LocalStorageService.set(['user', 'bearer'], { access_token: 'expired' });

    await rejectedLoad();

    expect(LocalStorageService.get(['user', 'bearer'])).toBeNull();
    expect(router.navigateByUrl).toHaveBeenCalledOnceWith(
      jasmine.stringMatching(/^\/login\?redirect=/),
    );
  });

  it('a second 401 does not navigate again', async () => {
    LocalStorageService.set(['user', 'bearer'], { access_token: 'expired' });

    await rejectedLoad();
    await rejectedLoad();

    expect(router.navigateByUrl).toHaveBeenCalledTimes(1);
  });

  it('a successful login re-arms the redirect', async () => {
    const auth = TestBed.inject(AuthService);
    LocalStorageService.set(['user', 'bearer'], { access_token: 'expired' });

    await rejectedLoad();

    auth.login('main', 'Passw0rd1').subscribe();
    http.expectOne(
      req => req.method === 'POST' && req.url === '/api/auth/token',
    ).flush(
      { code: 200, data: { access_token: 'fresh' } },
      { status: 200, statusText: 'OK' },
    );

    await rejectedLoad();

    expect(router.navigateByUrl).toHaveBeenCalledTimes(2);
  });
});
