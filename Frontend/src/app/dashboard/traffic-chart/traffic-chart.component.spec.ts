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

  function flushTraffic(target?: string) {
    const request = http.expectOne(req => req.url === '/api/vpn/traffic-data');
    if (target) expect(request.request.params.get('target')).toBe(target);
    request.flush(
      { code: 200, data: traffic_model },
      { status: 200, statusText: 'OK' },
    );
  }

  async function loadUser() {
    const promise = user_service.user();
    http.expectOne('/api/account/get-user').flush(
      { code: 200, data: user_payload },
      { status: 200, statusText: 'OK' },
    );
    await promise;
  }

  it('maps each traffic collection to a chart series with its daily labels (M11)', async () => {
    fixture.detectChanges();
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

  it('fetches with the active target when one is selected', async () => {
    await loadUser();
    user_service.setTraget('sub1');

    fixture.detectChanges();
    flushTraffic('sub1');
    await fixture.whenStable();

    expect(component.data?.datasets.length).toBe(3);
  });

  it('reloads the chart when the target user changes', async () => {
    await loadUser();

    fixture.detectChanges();
    flushTraffic();
    await fixture.whenStable();

    user_service.setTraget('sub1');
    flushTraffic('sub1');
    await fixture.whenStable();
  });
});
