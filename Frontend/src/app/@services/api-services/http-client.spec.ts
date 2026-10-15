import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { ResultStatus } from '../../@models';
import { LocalStorageService } from '../local-storage/local-storage-service';
import { HttpClientHandler } from './http-client';
import { API_BASE_URL } from './models/app-base-path';

describe('HttpClientHandler', () => {
  let handler: HttpClientHandler;
  let http: HttpTestingController;

  const transport_error_text =
    'ارتباط با سرور برقرار نشد؛ اتصال اینترنت یا در دسترس بودن سرور را بررسی کنید.';

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    handler = TestBed.inject(HttpClientHandler);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('prefixes the configured base path to every request', async () => {
    const result_promise = firstValueFrom(handler.get('/account/get-user'));
    const request = http.expectOne(req =>
      req.method === 'GET' && req.url === '/api/account/get-user'
    );
    request.flush({ code: 200, data: {} }, { status: 200, statusText: 'OK' });

    const result = await result_promise;
    expect(result.code).toBe(200);
  });

  it('maps result codes to status levels (L6)', async () => {
    const cases: [number, ResultStatus][] = [
      [100, ResultStatus.info],
      [204, ResultStatus.success],
      [302, ResultStatus.warning],
      [404, ResultStatus.error],
      [500, ResultStatus.serverFatal],
      [600, ResultStatus.uiFatal],
    ];

    for (const [code, expected] of cases) {
      const result_promise = firstValueFrom(handler.get('/probe'));
      http.expectOne('/api/probe').flush(
        { code, message: 'x' },
        { status: 200, statusText: 'OK' },
      );

      const result = await result_promise;
      expect(result.status()).withContext(`code ${code}`).toBe(expected);
    }
  });

  it('falls back to the HTTP status when the body carries no code', async () => {
    const result_promise = firstValueFrom(handler.get('/probe'));
    http.expectOne('/api/probe').flush(
      { message: 'not found' } as object,
      { status: 404, statusText: 'Not Found' },
    );

    const result = await result_promise;
    expect(result.code).toBe(404);
    expect(result.status()).toBe(ResultStatus.error);
  });

  it('escalates a 2xx body delivered over a failing http response to uiFatal', async () => {
    const result_promise = firstValueFrom(handler.get('/probe'));
    http.expectOne('/api/probe').flush(
      { code: 200, message: 'broken proxy' },
      { status: 502, statusText: 'Bad Gateway' },
    );

    const result = await result_promise;
    expect(result.code).toBe(600);
    expect(result.status()).toBe(ResultStatus.uiFatal);
  });

  it('sends the bearer token stored in local storage', async () => {
    LocalStorageService.set(['user', 'bearer'], { access_token: 'token-123' });

    const result_promise = firstValueFrom(handler.call('/probe'));
    const request = http.expectOne('/api/probe');
    expect(request.request.headers.get('Authorization')).toBe('Bearer token-123');
    request.flush({ code: 200 }, { status: 200, statusText: 'OK' });

    await result_promise;
  });

  it('omits the authorization header without a stored token', async () => {
    const result_promise = firstValueFrom(handler.call('/probe'));
    const request = http.expectOne('/api/probe');
    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush({ code: 200 }, { status: 200, statusText: 'OK' });

    await result_promise;
  });

  it('transport failure (status 0) reports the api.error-transport message as uiFatal', async () => {
    const result_promise = firstValueFrom(handler.call('/probe'));
    http.expectOne('/api/probe').error(new ProgressEvent('network down'), {
      status: 0,
      statusText: '',
    });

    const result = await result_promise;
    expect(result.code).toBe(600);
    expect(result.status()).toBe(ResultStatus.uiFatal);
    expect(result.message).toBe(transport_error_text);
  });
});
