import { provideHttpClient } from '@angular/common/http';
import { HttpRequest } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router, provideRouter } from '@angular/router';
import { AccountRole, AccountStatus, PagedResult, UserRow } from '../../@models';
import { API_BASE_URL, UserService } from '../../@services';
import { UsersComponent } from './users.component';

@Component({ template: '' })
class DummyComponent {}

describe('UsersComponent (docs/20 phase 6)', () => {
  let http: HttpTestingController;
  let router: Router;
  let user_service: UserService;
  let fixture: ComponentFixture<UsersComponent>;
  let component: UsersComponent;

  const page: PagedResult<UserRow> = {
    page: 1,
    pageSize: 20,
    totalCount: 2,
    totalPages: 1,
    items: [
      {
        username: 'sub1',
        name: 'Sub',
        surname: 'One',
        email: 's1@t.l',
        role: AccountRole.User,
        allowSelfService: true,
        status: AccountStatus.Active,
        owner: 'main',
        created: 1700000000000,
        createdTitle: '۱۴۰۲/۰۸/۰۱',
        simultaneousUser: 2,
        expirationDate: 1893456000000,
        trafficLeft: 25,
      },
      {
        username: 'sub2',
        name: 'Sub',
        surname: 'Two',
        email: 's2@t.l',
        role: AccountRole.User,
        allowSelfService: false,
        status: AccountStatus.Deactivated,
        created: 1700000000000,
        createdTitle: '۱۴۰۲/۰۸/۰۱',
      },
    ],
  };

  const primeUser = (role: AccountRole) => {
    const promise = user_service.user();
    http.expectOne('/api/account/get-user').flush(
      {
        code: 200,
        data: {
          username: 'main',
          fullname: 'Main',
          email: 'm@t.l',
          balance: 0,
          role,
          allowSelfService: true,
          targetArea: {
            main: { username: 'main', fullname: 'Main', email: 'm@t.l' },
            sub1: { username: 'sub1', fullname: 'Sub One', email: 's1@t.l' },
          },
        },
      },
      { status: 200, statusText: 'OK' },
    );
    return promise;
  };

  const flushList = (search?: string) => {
    const match = (req: HttpRequest<any>) =>
      req.method === 'GET' && req.url === '/api/user/list';
    const request = http.expectOne(match);
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('search')).toBe(search ?? null);
    request.flush(
      { code: 200, data: page },
      { status: 200, statusText: 'OK' },
    );
  };

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([
          { path: 'dashboard', component: DummyComponent },
        ]),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    user_service = TestBed.inject(UserService);
    fixture = TestBed.createComponent(UsersComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('loads the paged list and hides admin columns for a manager', async () => {
    await primeUser(AccountRole.Manager);

    fixture.detectChanges();
    flushList();

    expect(component.rows.length).toBe(2);
    expect(component.total).toBe(2);
    expect(component.isAdmin).toBeFalse();
    expect(component.displayedColumns).not.toContain('role');
    expect(component.displayedColumns).not.toContain('owner');
  });

  it('shows the role and owner columns for the admin', async () => {
    await primeUser(AccountRole.Admin);

    fixture.detectChanges();
    flushList();

    expect(component.isAdmin).toBeTrue();
    expect(component.displayedColumns).toContain('role');
    expect(component.displayedColumns).toContain('owner');
  });

  it('search sends the term to the server on page one', async () => {
    await primeUser(AccountRole.Manager);
    fixture.detectChanges();
    flushList();

    component.search = 'ali';
    component.applySearch();

    flushList('ali');
  });

  it('paging requests the next server page', async () => {
    await primeUser(AccountRole.Manager);
    fixture.detectChanges();
    flushList();

    component.onPage({ pageIndex: 2, pageSize: 20, length: 41 } as never);

    const request = http.expectOne(
      req => req.method === 'GET' && req.url === '/api/user/list',
    );
    expect(request.request.params.get('page')).toBe('3');
    request.flush(
      { code: 200, data: { ...page, page: 3, items: [] } },
      { status: 200, statusText: 'OK' },
    );
  });

  it('create posts the new subordinate with the initial password (manager: no role/owner)', async () => {
    await primeUser(AccountRole.Manager);
    fixture.detectChanges();
    flushList();

    component.openCreate();
    component.create_form.controls['username'].setValue('newuser');
    component.create_form.controls['password'].setValue('Passw0rd1');
    component.create_form.controls['email'].setValue('new@test.local');
    component.create_allow_self_service = true;
    component.submitCreate();

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/user/create',
    );
    expect(request.request.body).toEqual({
      username: 'newuser',
      password: 'Passw0rd1',
      email: 'new@test.local',
      mobile: undefined,
      firstname: undefined,
      lastname: undefined,
      allowSelfService: true,
    });
    request.flush(
      { code: 200, message: 'ساخته شد' },
      { status: 200, statusText: 'OK' },
    );

    // success reloads the first page
    flushList();
    expect(component.create_visible).toBeFalse();
  });

  it('create for the admin carries the explicit role and owner', async () => {
    await primeUser(AccountRole.Admin);
    fixture.detectChanges();
    flushList();

    component.openCreate();
    component.create_form.controls['username'].setValue('newuser');
    component.create_form.controls['password'].setValue('Passw0rd1');
    component.create_form.controls['email'].setValue('new@test.local');
    component.create_allow_self_service = false;
    component.create_role = AccountRole.Manager;
    component.create_owner = '  boss  ';
    component.submitCreate();

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/user/create',
    );
    expect(request.request.body['role']).toBe(AccountRole.Manager);
    expect(request.request.body['owner']).toBe('boss');
    expect(request.request.body['allowSelfService']).toBeFalse();
    request.flush(
      { code: 200, message: 'ساخته شد' },
      { status: 200, statusText: 'OK' },
    );

    flushList();
  });

  it('invalid create form never posts', async () => {
    await primeUser(AccountRole.Manager);
    fixture.detectChanges();
    flushList();

    component.openCreate();
    component.submitCreate();

    http.expectNone(
      req => req.method === 'POST' && req.url === '/api/user/create',
    );
  });

  it('set-status posts the target and the new status then reloads', async () => {
    await primeUser(AccountRole.Manager);
    fixture.detectChanges();
    flushList();

    // sub2 is Deactivated (2) → the full toggle reactivates it (0)
    component.toggleActive(component.rows[1]);

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/user/set-status',
    );
    expect(request.request.body).toEqual({
      target: 'sub2',
      status: AccountStatus.Active,
    });
    request.flush(
      { code: 200, message: 'فعال شد' },
      { status: 200, statusText: 'OK' },
    );

    flushList();
  });

  it('toggleVpn suspends an active account via set-status', async () => {
    await primeUser(AccountRole.Manager);
    fixture.detectChanges();
    flushList();

    component.toggleVpn(component.rows[0]);

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/user/set-status',
    );
    expect(request.request.body).toEqual({
      target: 'sub1',
      status: AccountStatus.VpnSuspended,
    });
    request.flush(
      { code: 200, message: 'تعلیق شد' },
      { status: 200, statusText: 'OK' },
    );

    flushList();
  });

  it('toggleVpn is a no-op for a fully deactivated account', async () => {
    await primeUser(AccountRole.Manager);
    fixture.detectChanges();
    flushList();

    component.toggleVpn(component.rows[1]);

    http.expectNone(
      req => req.method === 'POST' && req.url === '/api/user/set-status',
    );
  });

  it('toggleActive escalates a VPN-suspended account to full deactivation', async () => {
    await primeUser(AccountRole.Manager);
    fixture.detectChanges();
    flushList();

    const suspended: UserRow = { ...component.rows[0], status: AccountStatus.VpnSuspended };
    component.toggleActive(suspended);

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/user/set-status',
    );
    expect(request.request.body).toEqual({
      target: 'sub1',
      status: AccountStatus.Deactivated,
    });
    request.flush(
      { code: 200, message: 'غیرفعال شد' },
      { status: 200, statusText: 'OK' },
    );

    flushList();
  });

  it('set-self-service toggles the independence flag', async () => {
    await primeUser(AccountRole.Manager);
    fixture.detectChanges();
    flushList();

    component.toggleSelfService(component.rows[1]);

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/user/set-self-service',
    );
    expect(request.request.body).toEqual({ target: 'sub2', allow: true });
    request.flush(
      { code: 200, message: 'تغییر کرد' },
      { status: 200, statusText: 'OK' },
    );

    flushList();
  });

  it('set-owner posts null to detach an account', async () => {
    await primeUser(AccountRole.Admin);
    fixture.detectChanges();
    flushList();

    component.openOwner(component.rows[0]);
    component.new_owner = '   ';
    component.submitOwner();

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/user/set-owner',
    );
    expect(request.request.body).toEqual({ target: 'sub1', owner: null });
    request.flush(
      { code: 200, message: 'جدا شد' },
      { status: 200, statusText: 'OK' },
    );

    flushList();
  });

  it('password reset posts the chosen password for the target', async () => {
    await primeUser(AccountRole.Manager);
    fixture.detectChanges();
    flushList();

    component.openPassword(component.rows[0]);
    component.new_password = 'NewPassw0rd';
    component.submitPassword();

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/user/set-password',
    );
    expect(request.request.body).toEqual({
      target: 'sub1',
      password: 'NewPassw0rd',
    });
    request.flush(
      { code: 200, message: 'عوض شد' },
      { status: 200, statusText: 'OK' },
    );

    expect(component.password_visible).toBeFalse();
    expect(component.new_password).toBeUndefined();
  });

  it('weak passwords are refused before any request', async () => {
    await primeUser(AccountRole.Manager);
    fixture.detectChanges();
    flushList();

    component.openPassword(component.rows[0]);
    component.new_password = 'weak';
    component.submitPassword();

    http.expectNone(
      req => req.method === 'POST' && req.url === '/api/user/set-password',
    );
  });

  it('act on behalf switches the service target and opens the dashboard (manager)', async () => {
    await primeUser(AccountRole.Manager);
    fixture.detectChanges();
    flushList();

    const navigate_spy = spyOn(router, 'navigate').and.resolveTo(true);

    component.actOnBehalf(component.rows[0]);

    expect(user_service.targetName).toBe('sub1');
    expect(navigate_spy).toHaveBeenCalledWith(['/dashboard']);
  });

  it('act on behalf for the admin picks the external target (not the targetArea)', async () => {
    await primeUser(AccountRole.Admin);
    fixture.detectChanges();
    flushList();

    const navigate_spy = spyOn(router, 'navigate').and.resolveTo(true);

    // the row is not in the admin's targetArea, yet it must be selectable
    component.actOnBehalf(component.rows[0]);

    expect(user_service.targetName).toBe('sub1');
    expect(navigate_spy).toHaveBeenCalledWith(['/dashboard']);
  });

  it('acting on behalf of an inactive account is a no-op', async () => {
    await primeUser(AccountRole.Manager);
    fixture.detectChanges();
    flushList();

    const navigate_spy = spyOn(router, 'navigate').and.resolveTo(true);

    component.actOnBehalf(component.rows[1]);

    expect(navigate_spy).not.toHaveBeenCalled();
  });
});
