import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { UserVpnAccount } from '../../@models';
import { UserService } from '../../@services';
import { API_BASE_URL } from '../../@services/api-services/models/app-base-path';
import { DefaultHeaderComponent } from './default-header.component';

// smoke test (D6): the header renders once its user services resolve
describe('DefaultHeaderComponent', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<DefaultHeaderComponent>;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [DefaultHeaderComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([]),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(DefaultHeaderComponent);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  function answerUser(payload: Record<string, unknown>) {
    fixture.detectChanges();

    http.match(req => req.url === '/api/account/get-user').forEach(request =>
      request.flush(
        {
          code: 200,
          data: {
            targetArea: {
              main: { username: 'main', fullname: 'Main', email: 'm@t.l' },
            },
            ...payload,
          },
        },
        { status: 200, statusText: 'OK' },
      ),
    );

    return Promise.all([fixture.whenStable()]);
  }

  function expectEndAlignedNavCluster() {
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;
    const clusters = element.querySelectorAll('.ms-md-auto');
    expect(clusters.length).withContext('desktop auto margins').toBe(1);
    expect(clusters[0]?.contains(element.querySelector('a[routerLink="/dashboard"]'))).toBeTrue();

    expect(element.querySelectorAll('.ms-auto').length).withContext('phone auto margins').toBe(1);
  }

  it('should create', async () => {
    await answerUser({
      username: 'main',
      fullname: 'Main',
      email: 'm@t.l',
      balance: 0,
      role: 0,
      allowSelfService: true,
    });

    expect(fixture.componentInstance).toBeTruthy();
  });

  it('a dependent account (D6) sees no financial links or wallet balance', async () => {
    await answerUser({
      username: 'main',
      fullname: 'Main',
      email: 'm@t.l',
      balance: 0,
      role: 0,
      allowSelfService: false,
    });
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;
    // only dashboard + history remain (renewal, payment and wallet balance gone)
    expect(element.querySelectorAll('a[cNavLink]').length).toBe(2);
    expect(element.querySelector('a[routerLink="/renewal"]')).toBeNull();
    expect(element.textContent).not.toContain('کیف پول:');
    expect(fixture.componentInstance.isSelfService).toBeFalse();
  });

  it('a manager sees the users area but not the admin pages', async () => {
    await answerUser({
      username: 'main',
      fullname: 'Main',
      email: 'm@t.l',
      balance: 0,
      role: 1,
      allowSelfService: true,
    });
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;
    expect(element.querySelector('a[routerLink="/users"]')).not.toBeNull();
    expect(element.querySelector('a[routerLink="/servers"]')).toBeNull();
    expect(element.querySelector('a[routerLink="/wallet"]')).toBeNull();
    expect(fixture.componentInstance.isManager).toBeTrue();
  });

  it('the admin sees users, servers and wallet, and the selector without sub users', async () => {
    await answerUser({
      username: 'boss',
      fullname: 'Boss',
      email: 'b@t.l',
      balance: 0,
      role: 2,
      allowSelfService: true,
    });
    fixture.detectChanges();

    const component = fixture.componentInstance;
    const element: HTMLElement = fixture.nativeElement;

    expect(component.isAdmin).toBeTrue();
    expect(element.querySelector('a[routerLink="/users"]')).not.toBeNull();
    expect(element.querySelector('a[routerLink="/servers"]')).not.toBeNull();
    expect(element.querySelector('a[routerLink="/wallet"]')).not.toBeNull();

    // the admin always reaches the user-list selector (Docs/20 step 22)
    expect(component.canSelectTarget).toBeTrue();
  });

  it('a dependent account keeps the nav cluster end-aligned without the wallet block (L17)', async () => {
    await answerUser({
      username: 'main',
      fullname: 'Main',
      email: 'm@t.l',
      balance: 0,
      role: 0,
      allowSelfService: false,
    });

    expectEndAlignedNavCluster();
    expect(fixture.nativeElement.querySelector('c-alert')).toBeNull();
  });

  it('a self-service account keeps the wallet balance inside the end-aligned nav cluster (L17)', async () => {
    await answerUser({
      username: 'main',
      fullname: 'Main',
      email: 'm@t.l',
      balance: 0,
      role: 0,
      allowSelfService: true,
    });

    expectEndAlignedNavCluster();
    expect(fixture.nativeElement.querySelector('.ms-md-auto c-alert')).not.toBeNull();
  });

  it('the admin keeps a single end-aligned nav cluster (L17)', async () => {
    await answerUser({
      username: 'boss',
      fullname: 'Boss',
      email: 'b@t.l',
      balance: 0,
      role: 2,
      allowSelfService: true,
    });

    expectEndAlignedNavCluster();
  });

  it('keeps the manager wallet and lists the selected user VPN accounts', async () => {
    await answerUser({
      username: 'boss',
      fullname: 'Boss',
      email: 'b@t.l',
      balance: 500,
      role: 1,
      allowSelfService: true,
      accounts: [vpnAccount(7, 'boss-vpn'), vpnAccount(8, 'boss-spare')],
      targetArea: {
        boss: { username: 'boss', fullname: 'Boss', email: 'b@t.l' },
        sub1: { username: 'sub1', fullname: 'Sub One', email: 's@t.l' },
      },
    });
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;
    const balance = element.querySelector('c-alert')?.textContent;
    expect(balance).toContain('کیف پول:');
    expect(element.textContent).toContain('boss-vpn');
    expect(element.textContent).toContain('boss-spare');

    const cluster = element.querySelector('.ms-md-auto') as HTMLElement;
    const dashboard = cluster.querySelector('a[routerLink="/dashboard"]') as HTMLElement;
    const divider = cluster.querySelector('.vr') as HTMLElement;
    const wallet = cluster.querySelector('c-alert') as HTMLElement;
    expect(dashboard.compareDocumentPosition(divider) & Node.DOCUMENT_POSITION_FOLLOWING)
      .toBeTruthy();
    expect(divider.compareDocumentPosition(wallet) & Node.DOCUMENT_POSITION_FOLLOWING)
      .toBeTruthy();

    TestBed.inject(UserService).setTraget('sub1');
    http.expectOne((req) => req.url === '/api/vpn/accounts' && req.params.get('target') === 'sub1')
      .flush(
        { code: 200, data: [vpnAccount(51, 'sub-a'), vpnAccount(52, 'sub-b')] },
        { status: 200, statusText: 'OK' },
      );
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    expect(fixture.componentInstance.accountRows.map((account) => account.username))
      .toEqual(['sub-a', 'sub-b']);
    expect(element.textContent).toContain('sub-a');
    expect(element.textContent).toContain('sub-b');
    expect(element.textContent).not.toContain('boss-vpn');
    expect(element.textContent).not.toContain('boss-spare');
    expect(element.querySelector('c-alert')?.textContent).toBe(balance);

    const accountButton = Array.from(element.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('اکانت:'));
    const userButton = Array.from(element.querySelectorAll('button'))
      .find((button) => button.classList.contains('text-light') && button.textContent?.includes('sub1'));
    const home = element.querySelector('a[routerLink="/"]') as HTMLElement;
    const navCluster = element.querySelector('.ms-md-auto') as HTMLElement;
    expect(navCluster.contains(accountButton ?? null)).toBeFalse();
    expect(navCluster.contains(userButton ?? null)).toBeFalse();
    expect(home.compareDocumentPosition(userButton!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(userButton!.compareDocumentPosition(accountButton!) & Node.DOCUMENT_POSITION_FOLLOWING)
      .toBeTruthy();
    expect(accountButton?.classList.contains('btn-info')).toBeTrue();
    expect(accountButton?.classList.contains('dropdown-toggle')).toBeTrue();
    expect(accountButton?.classList.contains('text-light')).toBeFalse();
  });

  it('lists a selected user who has a single VPN account', async () => {
    await answerUser({
      username: 'boss',
      fullname: 'Boss',
      email: 'b@t.l',
      balance: 500,
      role: 1,
      allowSelfService: true,
      accounts: [vpnAccount(7, 'boss-vpn')],
      targetArea: {
        boss: { username: 'boss', fullname: 'Boss', email: 'b@t.l' },
        sub1: { username: 'sub1', fullname: 'Sub One', email: 's@t.l' },
      },
    });
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;
    expect(element.textContent).not.toContain('اکانت:');

    TestBed.inject(UserService).setTraget('sub1');
    http.expectOne((req) => req.url === '/api/vpn/accounts')
      .flush(
        { code: 200, data: [vpnAccount(51, 'only-sub')] },
        { status: 200, statusText: 'OK' },
      );
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    expect(fixture.componentInstance.showAccountMenu).toBeTrue();
    expect(element.textContent).toContain('only-sub');
    expect(element.textContent).not.toContain('boss-vpn');
  });
});

function vpnAccount(id: number, username: string): UserVpnAccount {
  return { id, username, realmId: 1, realmName: 'R1', status: 0, created: 0 };
}
