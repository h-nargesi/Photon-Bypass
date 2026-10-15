import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { API_BASE_URL } from '../@services/api-services/models/app-base-path';
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

  async function flushInitialLoad() {
    const drain = () => new Promise(resolve => setTimeout(resolve, 0));

    http.expectOne('/api/account/get-user').flush(
      { code: 200, data: user },
      { status: 200, statusText: 'OK' },
    );
    await drain();

    http.expectOne('/api/connection/current-con-state').flush(
      {
        code: 200,
        data: [
          { duration: 45, state: 1, server: 'srv1', sessionId: 'A1', closing: false },
          { duration: 125, state: 1, server: 'srv2', sessionId: 'B2', closing: false },
        ],
      },
      { status: 200, statusText: 'OK' },
    );
    http.expectOne('/api/plan/plan-state').flush(
      {
        code: 200,
        data: { remainsTimePercent: 40, remainsTrafficPercent: 20, simultaneousUserCount: 2 },
      },
      { status: 200, statusText: 'OK' },
    );

    // the dashboard embeds the traffic chart
    http.expectOne(req => req.url === '/api/vpn/traffic-data').flush(
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
