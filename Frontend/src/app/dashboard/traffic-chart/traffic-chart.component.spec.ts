import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { TrafficDataModel } from '../../@models';
import { UserService } from '../../@services';
import { API_BASE_URL } from '../../@services/api-services/models/app-base-path';
import { TrafficChartComponent } from './traffic-chart.component';

describe('TrafficChartComponent', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<TrafficChartComponent>;
  let component: TrafficChartComponent;
  let user_service: UserService;

  const traffic_model: TrafficDataModel = {
    title: 'ترافیک',
    labels: ['شنبه', 'یکشنبه', 'دوشنبه'],
    collections: [
      { title: 'Download', data: [10, 20, 30] },
      { title: 'Upload', data: [1, 2, 3] },
      { title: 'مجموع', data: [11, 22, 33] },
    ],
  };

  const user_payload = {
    username: 'main',
    fullname: 'Main',
    email: 'm@t.l',
    balance: 0,
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
    fixture = TestBed.createComponent(TrafficChartComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  function flushTraffic(accountId?: number) {
    const request = http.expectOne(req => req.url === '/api/vpn/traffic-data');
    if (accountId) expect(request.request.params.get('accountId')).toBe(String(accountId));
    request.flush(
      { code: 200, data: traffic_model },
      { status: 200, statusText: 'OK' },
    );
  }

  const drain = () => new Promise(resolve => setTimeout(resolve, 0));

  /// ngOnInit awaits the user before its first load
  async function openChart() {
    fixture.detectChanges();
    await drain();
  }

  async function loadUser(payload = user_payload) {
    const promise = user_service.user();
    http.expectOne('/api/account/get-user').flush(
      { code: 200, data: payload },
      { status: 200, statusText: 'OK' },
    );
    await promise;
  }

  it('maps each traffic collection to a chart series with its daily labels (M11)', async () => {
    await loadUser();
    await openChart();
    flushTraffic();
    await fixture.whenStable();

    expect(component.loading).toBeFalse();
    expect(component.data?.labels).toEqual(['شنبه', 'یکشنبه', 'دوشنبه']);

    const datasets = component.data!.datasets;
    expect(datasets.length).toBe(3);
    expect(datasets.map(dataset => dataset.label)).toEqual(['Download', 'Upload', 'مجموع']);
    expect(datasets[0].data).toEqual([10, 20, 30]);
    expect(datasets[2].data).toEqual([11, 22, 33]);
    expect(component.title).toBe('ترافیک');
  });

  it('fetches the selected user VPN account instead of the acting user', async () => {
    await loadUser();
    user_service.setTraget('sub1');

    await openChart();
    http.expectOne(
      (req) => req.url === '/api/vpn/accounts' && req.params.get('target') === 'sub1',
    ).flush(
      {
        code: 200,
        data: [{ id: 51, username: 'sub', realmId: 1, realmName: 'R1', status: 0, created: 0 }],
      },
      { status: 200, statusText: 'OK' },
    );
    await drain();
    flushTraffic(51);
    await fixture.whenStable();

    expect(component.data?.datasets.length).toBe(3);
  });

  it('reloads the chart when the account changes', async () => {
    await loadUser();

    await openChart();
    flushTraffic();
    await fixture.whenStable();

    user_service.setAccount(8);
    flushTraffic(8);
    await fixture.whenStable();

    expect(component.data?.datasets.length).toBe(3);
  });

  it('waits for the user instead of requesting without an account id', async () => {
    fixture.detectChanges();
    http.expectNone(req => req.url === '/api/vpn/traffic-data');

    await loadUser();
    await drain();
    flushTraffic(7);
    await fixture.whenStable();

    expect(component.data?.datasets.length).toBe(3);
  });

  it('requests nothing when the user owns no VPN account', async () => {
    await loadUser({ ...user_payload, accounts: [] });
    await openChart();

    http.expectNone(req => req.url === '/api/vpn/traffic-data');
    expect(component.loading).toBeFalse();
    expect(component.data).toBeUndefined();
  });
});
