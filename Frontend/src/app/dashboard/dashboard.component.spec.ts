import { HttpHeaders, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { API_BASE_URL } from '../@services/api-services/models/app-base-path';
import { LocalStorageService } from '../@services';
import { DashboardComponent } from './dashboard.component';

describe('DashboardComponent', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<DashboardComponent>;
  let component: DashboardComponent;

  const user = {
    username: 'main',
    fullname: 'Main',
    email: 'main@test.local',
    balance: 500,
    targetArea: {
      main: { username: 'main', fullname: 'Main', email: 'm@t.l' },
      sub1: { username: 'sub1', fullname: 'Sub', email: 's@t.l' },
    },
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
    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  async function flushInitialLoad(capabilities?: object) {
    const drain = () => new Promise(resolve => setTimeout(resolve, 0));

    http.expectOne(req => req.url === '/api/account/get-user').flush(
      { code: 200, data: user },
      { status: 200, statusText: 'OK' },
    );
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
          target: 'main',
          days: 120,
          gigabytes: 50,
          simultaneousUserCount: 2,
          kind: 'usermanager',
          supportsOvpnCert: true,
          supportsPasswordChange: true,
        },
      },
      { status: 200, statusText: 'OK' },
    );

    // the dashboard embeds the traffic chart (it fires one plain and one targeted load)
    for (const request of http.match(req => req.url === '/api/vpn/traffic-data')) {
      request.flush(
        { code: 200, data: { title: 't', labels: [], collections: [] } },
        { status: 200, statusText: 'OK' },
      );
    }
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
      target: 'main',
      kind: 'usermanager',
      supportsOvpnCert: false,
      supportsPasswordChange: true,
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
      target: 'main',
      kind: 'xui',
      supportsOvpnCert: false,
      supportsPasswordChange: false,
    });
    await fixture.whenStable();

    expect(queryButtons('a.icon-btn.pointer').length).toBe(0);

    const links = queryButtons('a.icon-btn:not(.pointer)');
    expect(links.length).toBe(1); // only edit-user-info
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
      req => req.method === 'GET' && req.url === '/api/vpn/download-cert',
    );
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob(['cert-body'], { type: 'application/x-openvpn-profile' }), {
      status: 200,
      statusText: 'OK',
      headers: new HttpHeaders({
        'Content-Type': 'application/x-openvpn-profile',
        'Content-Disposition': 'attachment; filename="main.ovpn"',
      }),
    });
    expect(component.downloading_cert).toBeFalse();
    expect(anchors.clicked?.download).toBe('main.ovpn');
    expect(create_spy).toHaveBeenCalled();
    expect(revoke_spy).toHaveBeenCalled();
  });

  it('stops the spinner and reports the server message when the download fails', async () => {
    fixture.detectChanges();
    await flushInitialLoad();
    await fixture.whenStable();

    component.downloadCertificate();

    const request = http.expectOne(
      req => req.method === 'GET' && req.url === '/api/vpn/download-cert',
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

    const traffic_risk = { ...component.plan_info!, remainsTimePercent: 10, remainsTrafficPercent: 50 };
    expect(component.getPlanInfoColor(traffic_risk)).toBe('info');
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
});
