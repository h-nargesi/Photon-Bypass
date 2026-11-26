import { HttpHeaders, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { API_BASE_URL } from '../@services/api-services/models/app-base-path';
import { PlanInfo } from '../@models';
import { LocalStorageService, TranslationService, UserService } from '../@services';
import { DashboardComponent } from './dashboard.component';

describe('DashboardComponent', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<DashboardComponent>;
  let component: DashboardComponent;
  let user_service: UserService;

  const user = {
    username: 'main',
    fullname: 'Main',
    email: 'main@test.local',
    balance: 500,
    targetArea: {
      main: { username: 'main', fullname: 'Main', email: 'm@t.l' },
      sub1: { username: 'sub1', fullname: 'Sub', email: 's@t.l' },
    },
    accounts: [
      { id: 7, username: 'main', realmId: 1, realmName: 'R1', status: 0, created: 0 },
      { id: 8, username: 'main2', realmId: 2, realmName: 'R2', status: 0, created: 0 },
    ],
  };

  beforeEach(() => {
    localStorage.clear();
    LocalStorageService.set(['user', 'target'], 'main');
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([]),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    user_service = TestBed.inject(UserService);
    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
  });

  function flushSupportConfig() {
    http
      .match(req => req.url === '/api/basics/app-config')
      .forEach(request =>
        request.flush(
          {
            code: 200,
            data: {
              allowRegistration: false,
              supportContactInfo: '@support',
              supportDescription: 'پشتیبانی آنلاین',
            },
          },
          { status: 200, statusText: 'OK' },
        ),
      );
  }

  afterEach(() => {
    flushSupportConfig();
    http.verify();
    localStorage.clear();
  });

  async function flushInitialLoad(
    capabilities?: Partial<PlanInfo>,
    payload: Record<string, unknown> = user,
  ) {
    const drain = () => new Promise(resolve => setTimeout(resolve, 0));

    http.expectOne(req => req.url === '/api/account/get-user').flush(
      { code: 200, data: payload },
      { status: 200, statusText: 'OK' },
    );
    await drain();
    // the account is known now: the dashboard creates the traffic chart
    fixture.detectChanges();
    await drain();

    http.expectOne(req => req.url === '/api/connection/current-con-state').flush(
      {
        code: 200,
        data: [
          { duration: 45, state: 1, server: 'srv1', sessionId: 'A1', closing: false },
          { duration: 125, state: 1, server: 'srv2', sessionId: 'B2', closing: false },
        ],
      },
      { status: 200, statusText: 'OK' },
    );
    http.expectOne(req => req.url === '/api/plan/plan-state').flush(
      {
        code: 200,
        data: { remainsTimePercent: 40, remainsTrafficPercent: 20, simultaneousUserCount: 2 },
      },
      { status: 200, statusText: 'OK' },
    );
    http.expectOne(req => req.url === '/api/plan/plan-info').flush(
      {
        code: 200,
        data: capabilities ?? {
          accountId: 7,
          days: 120,
          gigabytes: 50,
          simultaneousUserCount: 2,
          kind: 'usermanager',
          supportsOvpnCert: true,
          supportsPasswordChange: true,
          supportsCredentialRotation: false,
        },
      },
      { status: 200, statusText: 'OK' },
    );

    if (capabilities?.supportsCredentialRotation) {
      http.expectOne(req => req.url === '/api/vpn/config-info').flush(
        {
          code: 200,
          data: {
            kind: 'xui',
            vlessLink: 'vless://uuid-1@srv21.photon-bypass.com:443?type=ws',
          },
        },
        { status: 200, statusText: 'OK' },
      );
    }

    // the embedded traffic chart waits for the user: one targeted load, never
    // an id-less one (the account-scoped endpoints reject a missing accountId)
    const traffic = http.expectOne(req => req.url === '/api/vpn/traffic-data');
    expect(traffic.request.params.get('accountId')).toBe('7');
    traffic.flush(
      { code: 200, data: { title: 't', labels: [], collections: [] } },
      { status: 200, statusText: 'OK' },
    );
    await drain();
  }

  it('loads the user, connections and plan-state on start', async () => {
    fixture.detectChanges();
    await flushInitialLoad();
    await fixture.whenStable();

    expect(component.current_user?.username).toBe('main');
    expect(component.connections?.length).toBe(2);
    expect(component.connection_count).toBe('2');
    expect(component.plan_info?.remainsTimePercent).toBe(40);
  });

  it('loads plan-state for the selected subordinate VPN account', async () => {
    fixture.detectChanges();
    await flushInitialLoad();
    await fixture.whenStable();

    await flushSubordinateLoad();

    expect(component.plan_info?.remainsTimePercent).toBe(10);
  });

  async function flushSubordinateLoad() {
    user_service.setTraget('sub1');

    http.expectOne(
      (req) => req.url === '/api/vpn/accounts' && req.params.get('target') === 'sub1',
    ).flush(
      {
        code: 200,
        data: [{ id: 51, username: 'sub-vpn', realmId: 3, realmName: 'R3', status: 0, created: 0 }],
      },
      { status: 200, statusText: 'OK' },
    );
    await new Promise((resolve) => setTimeout(resolve, 0));

    const plan = http.expectOne((req) => req.url === '/api/plan/plan-state');
    expect(plan.request.params.get('accountId')).toBe('51');
    plan.flush(
      {
        code: 200,
        data: { remainsTimePercent: 10, remainsTrafficPercent: 5, simultaneousUserCount: 1 },
      },
      { status: 200, statusText: 'OK' },
    );
    http.expectOne((req) => req.url === '/api/connection/current-con-state').flush(
      { code: 200, data: [] },
      { status: 200, statusText: 'OK' },
    );
    http.expectOne((req) => req.url === '/api/plan/plan-info').flush(
      {
        code: 200,
        data: {
          accountId: 51,
          days: 30,
          gigabytes: 10,
          simultaneousUserCount: 1,
          supportsCredentialRotation: false,
        },
      },
      { status: 200, statusText: 'OK' },
    );
    const traffic = http.expectOne((req) => req.url === '/api/vpn/traffic-data');
    expect(traffic.request.params.get('accountId')).toBe('51');
    traffic.flush(
      { code: 200, data: { title: 't', labels: [], collections: [] } },
      { status: 200, statusText: 'OK' },
    );
  }

  it('exposes the plan capabilities from plan-info', async () => {
    fixture.detectChanges();
    await flushInitialLoad();
    await fixture.whenStable();

    expect(component.plan_capabilities?.kind).toBe('usermanager');
    expect(component.plan_capabilities?.supportsOvpnCert).toBeTrue();
    expect(component.plan_capabilities?.supportsPasswordChange).toBeTrue();
  });

  function queryButtons(selector: string): HTMLElement[] {
    fixture.detectChanges();
    return Array.from(
      fixture.nativeElement.querySelectorAll(selector),
    ) as HTMLElement[];
  }

  it('hides the ovpn buttons until the plan reports cert support', async () => {
    fixture.detectChanges();
    await flushInitialLoad({
      accountId: 7,
      kind: 'usermanager',
      supportsOvpnCert: false,
      supportsPasswordChange: true,
      supportsCredentialRotation: false,
    });
    await fixture.whenStable();

    // the email/download buttons carry the pointer class, router links do not
    expect(queryButtons('a.icon-btn.pointer').length).toBe(0);
    const links = queryButtons('a.icon-btn:not(.pointer)').map(btn => btn.textContent);
    expect(links.length).toBe(2); // change-password + edit-user-info
  });

  it('hides the change-password link when the data-plane forbids it', async () => {
    fixture.detectChanges();
    await flushInitialLoad({
      accountId: 7,
      kind: 'xui',
      supportsOvpnCert: false,
      supportsPasswordChange: false,
      supportsCredentialRotation: false,
    });
    await fixture.whenStable();

    expect(queryButtons('a.icon-btn.pointer').length).toBe(0);

    const links = queryButtons('a.icon-btn:not(.pointer)');
    expect(links.length).toBe(1); // only edit-user-info
  });

  it('renders the vless link row and rotation entry for xui data-planes', async () => {
    fixture.detectChanges();
    await flushInitialLoad({
      accountId: 7,
      kind: 'xui',
      supportsOvpnCert: false,
      supportsPasswordChange: false,
      supportsCredentialRotation: true,
    });
    await fixture.whenStable();

    expect(component.vlessLink).toBe(
      'vless://uuid-1@srv21.photon-bypass.com:443?type=ws',
    );

    fixture.detectChanges();
    const rendered_link = fixture.nativeElement.querySelector('.vless-link code');
    expect(rendered_link?.textContent).toContain('vless://uuid-1@srv21.photon-bypass.com');

    // copy + email + download carry the pointer class; rotate is a router link
    expect(queryButtons('a.icon-btn.pointer').length).toBe(3);
    const rotate_links = queryButtons('a[routerlink="/rotate-ovpn-id"]');
    expect(rotate_links.length).toBe(1);
  });

  it('copies the vless link to the clipboard on demand', async () => {
    fixture.detectChanges();
    await flushInitialLoad({
      accountId: 7,
      kind: 'xui',
      supportsOvpnCert: false,
      supportsPasswordChange: false,
      supportsCredentialRotation: true,
    });
    await fixture.whenStable();

    const clipboard_spy = spyOn(navigator.clipboard, 'writeText').and.resolveTo(
      undefined as never,
    );

    component.copyVlessLink();
    await new Promise(resolve => setTimeout(resolve, 0));

    expect(clipboard_spy).toHaveBeenCalledWith(
      'vless://uuid-1@srv21.photon-bypass.com:443?type=ws',
    );
    expect(component.copying_vless_link).toBeFalse();
  });

  it('resets the copy state and reports an error when the clipboard rejects', async () => {
    fixture.detectChanges();
    await flushInitialLoad({
      accountId: 7,
      kind: 'xui',
      supportsOvpnCert: false,
      supportsPasswordChange: false,
      supportsCredentialRotation: true,
    });
    await fixture.whenStable();

    spyOn(navigator.clipboard, 'writeText').and.rejectWith(
      new DOMException('denied') as never,
    );

    component.copyVlessLink();
    await new Promise(resolve => setTimeout(resolve, 0));

    expect(component.copying_vless_link).toBeFalse();
  });

  it('never fetches config-info for a non-xui data-plane', async () => {
    fixture.detectChanges();
    await flushInitialLoad();
    await fixture.whenStable();

    http.expectNone(req => req.url === '/api/vpn/config-info');
    expect(component.config_info).toBeUndefined();
  });

  it('shows the cert email and download buttons when the plan supports ovpn', async () => {
    fixture.detectChanges();
    await flushInitialLoad();
    await fixture.whenStable();

    // send-to-user + download (send-to-me stays hidden for the own account)
    expect(queryButtons('a.icon-btn.pointer').length).toBe(2);
    expect(queryButtons('a.icon-btn:not(.pointer)').length).toBe(2);
  });

  it('downloads the certificate as a blob file', async () => {
    fixture.detectChanges();
    await flushInitialLoad();
    await fixture.whenStable();

    const anchors = { clicked: undefined as HTMLAnchorElement | undefined };
    const original_create = document.createElement.bind(document);
    spyOn(document, 'createElement').and.callFake((tag?: string) => {
      const element = original_create(tag ?? 'div');
      if (tag === 'a' && element instanceof HTMLAnchorElement) {
        anchors.clicked = element;
        element.click = () => undefined;
      }
      return element;
    });
    const revoke_spy = spyOn(URL, 'revokeObjectURL').and.stub();
    const create_spy = spyOn(URL, 'createObjectURL').and.callThrough();

    component.downloadCertificate();

    const request = http.expectOne(
      req => req.method === 'GET' && req.url === '/api/vpn/download',
    );
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob(['zip-body'], { type: 'application/zip' }), {
      status: 200,
      statusText: 'OK',
      headers: new HttpHeaders({
        'Content-Type': 'application/zip',
        'Content-Disposition': 'attachment; filename="main.zip"',
      }),
    });
    expect(component.downloading_cert).toBeFalse();
    expect(anchors.clicked?.download).toBe('main.zip');
    expect(create_spy).toHaveBeenCalled();
    expect(revoke_spy).toHaveBeenCalled();
  });

  it('stops the spinner and reports the server message when the download fails', async () => {
    fixture.detectChanges();
    await flushInitialLoad();
    await fixture.whenStable();

    component.downloadCertificate();

    const request = http.expectOne(
      req => req.method === 'GET' && req.url === '/api/vpn/download',
    );
    request.flush(new Blob(['{"code":400,"message":"محدود"}']), {
      status: 400,
      statusText: 'Bad Request',
    });

    await new Promise(resolve => setTimeout(resolve, 0));

    expect(component.downloading_cert).toBeFalse();
  });

  it('formats connection durations in minutes and hours', async () => {
    fixture.detectChanges();
    await flushInitialLoad();
    await fixture.whenStable();

    expect(component.printDuration(45)).toContain('45');
    expect(component.printDuration(125)).toContain('2');
  });

  it('flags the plan as time-risk when time runs out faster than traffic', async () => {
    fixture.detectChanges();
    await flushInitialLoad();
    await fixture.whenStable();

    expect(component.getPlanInfoColor(component.plan_info)).toBe('warning');
    expect(component.getPlanInfoTooltip(component.plan_info))
      .toBe(TranslationService.translate('dashboard.balance.monthly-risk'));

    const traffic_risk = { ...component.plan_info!, remainsTimePercent: 10, remainsTrafficPercent: 50 };
    expect(component.getPlanInfoColor(traffic_risk)).toBe('info');
    expect(component.getPlanInfoTooltip(traffic_risk))
      .toBe(TranslationService.translate('dashboard.balance.traffic-risk'));
  });

  it('closing a connection removes it from the list', async () => {
    fixture.detectChanges();
    await flushInitialLoad();
    await fixture.whenStable();

    component.closeConnection(0);

    expect(component.connections![0].closing).toBeTrue();

    http.expectOne(
      req => req.method === 'POST' && req.url === '/api/connection/close-con',
    ).flush({ code: 200 }, { status: 200, statusText: 'OK' });

    expect(component.connections?.length).toBe(1);
    expect(component.connections![0].sessionId).toBe('B2');
    expect(component.connection_count).toBe('1');
  });

  async function loadUserWithoutAccounts(user: Record<string, unknown>) {
    const drain = () => new Promise(resolve => setTimeout(resolve, 0));

    fixture.detectChanges();
    http.expectOne(req => req.url === '/api/account/get-user').flush(
      { code: 200, data: user },
      { status: 200, statusText: 'OK' },
    );
    await drain();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('loads nothing per-account and offers the first purchase without any VPN account', async () => {
    await loadUserWithoutAccounts({ ...user, accounts: [], allowSelfService: true });

    http.expectNone(req => /\/api\/(vpn|plan|connection)\//.test(req.url));
    expect(component.hasAccount).toBeFalse();
    expect(component.plan_info).toBeUndefined();
    expect(component.connections).toEqual([]);

    const buy = fixture.nativeElement.querySelector('[routerlink="/renewal"]');
    expect(buy?.textContent).toContain(TranslationService.translate('dashboard.balance.buy'));
    expect(fixture.nativeElement.querySelector('.placeholder')).toBeNull();
    expect(fixture.nativeElement.querySelector('c-spinner')).toBeNull();
    expect(fixture.nativeElement.querySelector('app-traffic-chart')).toBeNull();
  });

  it('offers no purchase to a dependent user without any VPN account', async () => {
    await loadUserWithoutAccounts({ ...user, accounts: [] });

    expect(fixture.nativeElement.querySelector('[routerlink="/renewal"]')).toBeNull();
  });

  it('hides the support card from a dependent user viewing himself (D6)', async () => {
    fixture.detectChanges();
    await flushInitialLoad(undefined, { ...user, allowSelfService: false });
    flushSupportConfig();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(component.showsSupportCard).toBeFalse();
    expect(fixture.nativeElement.querySelector('.telegram-panel')).toBeNull();
  });

  it('shows the support card with the backend contact info for a self-service user', async () => {
    fixture.detectChanges();
    await flushInitialLoad(undefined, { ...user, allowSelfService: true });
    flushSupportConfig();
    fixture.detectChanges();
    await fixture.whenStable();

    const card: HTMLElement = fixture.nativeElement.querySelector('.telegram-panel');
    expect(component.showsSupportCard).toBeTrue();
    expect(card.textContent).toContain('@support');
    expect(card.textContent).toContain('پشتیبانی آنلاین');
    expect(card.textContent).toContain(TranslationService.translate('dashboard.support.title'));
  });

  it('keeps the support card in an on-behalf view of an actor without self-service', async () => {
    fixture.detectChanges();
    await flushInitialLoad(undefined, { ...user, role: 1, allowSelfService: false });
    flushSupportConfig();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(fixture.nativeElement.querySelector('.telegram-panel')).toBeNull();

    await flushSubordinateLoad();
    fixture.detectChanges();

    expect(component.showsSupportCard).toBeTrue();
    expect(fixture.nativeElement.querySelector('.telegram-panel')).not.toBeNull();
  });

  const outerBands = ':scope > c-row > c-col';
  const topBand = ':scope > c-row > c-col:first-child > c-row > c-col';

  function columns(selector: string): HTMLElement[] {
    return Array.from(
      fixture.nativeElement.querySelectorAll(selector),
    ) as HTMLElement[];
  }

  function gridClasses(selector: string): string[][] {
    return columns(selector).map((col) => Array.from(col.classList).sort());
  }

  it('keeps the plan widget beside the support card for a self-service user', async () => {
    fixture.detectChanges();
    await flushInitialLoad(undefined, { ...user, allowSelfService: true });
    flushSupportConfig();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(gridClasses(topBand)).toEqual([['col-lg-6'], ['col-lg-6']]);
    const [support_col, plan_col] = columns(topBand);
    expect(support_col.querySelector('.telegram-panel')).not.toBeNull();
    expect(plan_col.querySelector('c-widget-stat-c')).not.toBeNull();
  });

  it('lets the plan widget own the whole top band for a dependent user', async () => {
    fixture.detectChanges();
    await flushInitialLoad(undefined, { ...user, allowSelfService: false });
    flushSupportConfig();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(gridClasses(topBand)).toEqual([['col-lg-12']]);
    expect(columns(topBand)[0].querySelector('c-widget-stat-c')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.telegram-panel')).toBeNull();
  });

  it('never moves the connections, account and chart columns when the support card toggles', async () => {
    fixture.detectChanges();
    await flushInitialLoad(undefined, { ...user, role: 1, allowSelfService: false });
    flushSupportConfig();
    fixture.detectChanges();
    await fixture.whenStable();

    const bands_below = [['col-lg-6'], ['col-lg-6'], ['col']];
    expect(gridClasses(outerBands)).toEqual([['col-12'], ...bands_below]);
    expect(gridClasses(topBand)).toEqual([['col-lg-12']]);

    await flushSubordinateLoad();
    fixture.detectChanges();

    expect(gridClasses(topBand)).toEqual([['col-lg-6'], ['col-lg-6']]);
    expect(gridClasses(outerBands)).toEqual([['col-12'], ...bands_below]);
  });
});
