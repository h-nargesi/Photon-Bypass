import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { API_BASE_URL } from '../api-services/models/app-base-path';
import { AppConfigService } from './app-config-service';

describe('AppConfigService', () => {
  let service: AppConfigService;
  let http: HttpTestingController;

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
    service = TestBed.inject(AppConfigService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('fetches the public app-config (D11)', () => {
    let config: { allowRegistration: boolean } | null | undefined;

    service.config().subscribe(data => (config = data));

    http
      .expectOne(req => req.method === 'GET' && req.url === '/api/basics/app-config')
      .flush(
        { code: 200, data: { allowRegistration: false } },
        { status: 200, statusText: 'OK' },
      );

    expect(config).toEqual({ allowRegistration: false });
  });

  it('returns null data without toasting on transport failure', () => {
    let config: { allowRegistration: boolean } | null | undefined;

    service.config().subscribe(data => (config = data));

    http
      .expectOne(req => req.url === '/api/basics/app-config')
      .flush(
        { code: 0, message: 'offline' },
        { status: 0, statusText: 'Offline' },
      );

    expect(config).toBeNull();
  });
});
