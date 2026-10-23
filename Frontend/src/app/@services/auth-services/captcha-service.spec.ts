import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CaptchaService } from './captcha-service';
import { API_BASE_URL } from '../api-services/models/app-base-path';

describe('CaptchaService', () => {
  let http: HttpTestingController;
  let service: CaptchaService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(CaptchaService);
  });

  afterEach(() => {
    http.verify();
  });

  it('fetch stores the current challenge', () => {
    service.fetch().subscribe();

    http.expectOne(req => req.method === 'GET' && req.url === '/api/auth/captcha').flush(
      { code: 200, data: { id: 'cap-1', image: 'data:image/png;base64,AAA', expiresInSeconds: 180 } },
      { status: 200, statusText: 'OK' },
    );

    expect(service.headers('ab2cd')).toEqual({
      'X-Captcha-Id': 'cap-1',
      'X-Captcha-Code': 'ab2cd',
    });
  });

  it('headers are absent without a challenge or a code', () => {
    expect(service.headers('ab2cd')).toBeUndefined();

    service.fetch().subscribe();
    http.expectOne(req => req.url === '/api/auth/captcha').flush(
      { code: 200, data: { id: 'cap-1', image: 'img', expiresInSeconds: 180 } },
      { status: 200, statusText: 'OK' },
    );

    expect(service.headers(undefined)).toBeUndefined();
    expect(service.headers('   ')).toBeUndefined();
  });

  it('the code is trimmed before it travels in the header', () => {
    service.fetch().subscribe();
    http.expectOne(req => req.url === '/api/auth/captcha').flush(
      { code: 200, data: { id: 'cap-1', image: 'img', expiresInSeconds: 180 } },
      { status: 200, statusText: 'OK' },
    );

    expect(service.headers('  AB2CD ')).toEqual({
      'X-Captcha-Id': 'cap-1',
      'X-Captcha-Code': 'AB2CD',
    });
  });

  it('clear drops the challenge', () => {
    service.fetch().subscribe();
    http.expectOne(req => req.url === '/api/auth/captcha').flush(
      { code: 200, data: { id: 'cap-1', image: 'img', expiresInSeconds: 180 } },
      { status: 200, statusText: 'OK' },
    );

    service.clear();

    expect(service.headers('ab2cd')).toBeUndefined();
  });
});
