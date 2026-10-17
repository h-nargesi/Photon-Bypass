import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CanActivateFn, UrlTree } from '@angular/router';
import { Router, provideRouter } from '@angular/router';
import { AccountRole, UserModel } from '../../@models';
import { API_BASE_URL } from '../api-services/models/app-base-path';
import { UserService } from './user-servcie';
import {
  adminGuard,
  managerGuard,
  selfServiceGuard,
} from './role-guards';

describe('role guards (docs/20 phase 6)', () => {
  let http: HttpTestingController;
  let router: Router;

  const user = (role: AccountRole, allow_self_service: boolean): UserModel => ({
    username: 'someone',
    fullname: 'Some One',
    email: 's@t.l',
    balance: 0,
    role,
    allowSelfService: allow_self_service,
    targetArea: {
      someone: { username: 'someone', fullname: 'Some One', email: 's@t.l' },
    },
  });

  const flush_user = (model: UserModel) => {
    http.expectOne('/api/account/get-user').flush(
      { code: 200, data: model },
      { status: 200, statusText: 'OK' },
    );
  };

  const run = (guard: CanActivateFn) =>
    TestBed.runInInjectionContext(() =>
      guard({} as never, { url: '/target-url' } as never),
    );

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
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('managerGuard admits manager and admin', async () => {
    const first = run(managerGuard);
    flush_user(user(AccountRole.Manager, true));
    expect(await first).toBeTrue();

    // the cached user must not leak between lookups — force a fresh fetch
    TestBed.inject(UserService).clear();
    const second = run(managerGuard);
    flush_user(user(AccountRole.Admin, true));
    expect(await second).toBeTrue();
  });

  it('managerGuard sends plain and dependent users back to the dashboard', async () => {
    const result = run(managerGuard);
    flush_user(user(AccountRole.User, true));

    expect(await result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl((await result) as UrlTree)).toBe('/dashboard');
  });

  it('adminGuard admits only the admin', async () => {
    const manager_result = run(adminGuard);
    flush_user(user(AccountRole.Manager, true));
    expect(router.serializeUrl((await manager_result) as UrlTree)).toBe('/dashboard');

    TestBed.inject(UserService).clear();
    const admin_result = run(adminGuard);
    flush_user(user(AccountRole.Admin, true));
    expect(await admin_result).toBeTrue();
  });

  it('selfServiceGuard mirrors D6: dependent accounts are redirected', async () => {
    const dependent_result = run(selfServiceGuard);
    flush_user(user(AccountRole.User, false));
    expect(router.serializeUrl((await dependent_result) as UrlTree)).toBe('/dashboard');

    TestBed.inject(UserService).clear();
    const independent_result = run(selfServiceGuard);
    flush_user(user(AccountRole.User, true));
    expect(await independent_result).toBeTrue();
  });

  it('a failed user lookup redirects to the login with the return url', async () => {
    const result = run(managerGuard);
    http.expectOne('/api/account/get-user').flush(
      { code: 401 },
      { status: 200, statusText: 'OK' },
    );

    expect(router.serializeUrl((await result) as UrlTree)).toBe('/login?redirect=%2Ftarget-url');
  });
});
