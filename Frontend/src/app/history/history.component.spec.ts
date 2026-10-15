import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { UserService } from '../@services';
import { API_BASE_URL } from '../@services/api-services/models/app-base-path';
import { HistoryComponent } from './history.component';

describe('HistoryComponent', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<HistoryComponent>;
  let component: HistoryComponent;
  let user_service: UserService;

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
    fixture = TestBed.createComponent(HistoryComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  function flushHistory() {
    http.expectOne(req => req.url === '/api/account/history').flush(
      { code: 200, data: [] },
      { status: 200, statusText: 'OK' },
    );
  }

  async function loadUser() {
    const promise = user_service.user();
    http.expectOne('/api/account/get-user').flush(
      {
        code: 200,
        data: {
          username: 'main',
          fullname: 'Main',
          email: 'm@t.l',
          balance: 0,
          targetArea: {
            main: { username: 'main', fullname: 'Main', email: 'm@t.l' },
            sub1: { username: 'sub1', fullname: 'Sub', email: 's@t.l' },
          },
        },
      },
      { status: 200, statusText: 'OK' },
    );
    await promise;
  }

  it('loads history without date filters by default', async () => {
    fixture.detectChanges();

    http.expectOne(req => req.url === '/api/account/history')
      .flush({ code: 200, data: [] }, { status: 200, statusText: 'OK' });
    await fixture.whenStable();

    expect(component.dataSource).toBeDefined();
  });

  it('sends from/to filters as epoch milliseconds with end-of-day bound', async () => {
    await loadUser();
    fixture.detectChanges();
    flushHistory();
    await fixture.whenStable();

    component.fromDate = new Date(2026, 0, 10);
    component.toDate = new Date(2026, 0, 12);
    component.loadWithFilter();

    const request = http.expectOne(req => req.url === '/api/account/history');
    expect(Number(request.request.params.get('from'))).toBe(
      new Date(2026, 0, 10).getTime(),
    );
    expect(Number(request.request.params.get('to'))).toBe(
      new Date(2026, 0, 12, 23, 59, 59, 999).getTime(),
    );

    request.flush({ code: 200, data: [] }, { status: 200, statusText: 'OK' });
  });

  it('passes the active target as query parameter', async () => {
    await loadUser();
    user_service.setTraget('sub1');

    fixture.detectChanges();
    const request = http.expectOne(req => req.url === '/api/account/history');
    expect(request.request.params.get('target')).toBe('sub1');

    request.flush({ code: 200, data: [] }, { status: 200, statusText: 'OK' });
  });
});
