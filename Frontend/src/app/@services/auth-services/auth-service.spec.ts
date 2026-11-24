import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AccountRole, ApiResult, ResultStatus, UserModel } from '../../@models';
import { ApiBaseService } from '../api-services/api-base-service';
import { API_BASE_URL } from '../api-services/models/app-base-path';
import { LocalStorageService } from '../local-storage/local-storage-service';
import { MessageService } from '../message-handler/message-service';
import { AuthService } from './auth-service';
import { UserService } from './user-servcie';

class RedirectLatch extends ApiBaseService {
  static arm(): void {
    ApiBaseService.is_logout = false;
  }
}

describe('AuthService', () => {
  let http: HttpTestingController;
  let service: AuthService;
  let users: UserService;

  const user: UserModel = {
    username: 'main',
    fullname: 'Main User',
    email: 'main@test.local',
    balance: 100,
    role: AccountRole.User,
    allowSelfService: true,
    accounts: [],
    targetArea: {
      main: { username: 'main', fullname: 'Main User', email: 'main@test.local' },
    },
  };

  const ok = { status: 200, statusText: 'OK' };
  const unauthorized = { status: 401, statusText: 'Unauthorized' };

  const bearer = () => LocalStorageService.get(['user', 'bearer']);

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
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(AuthService);
    users = TestBed.inject(UserService);
    RedirectLatch.arm();
  });

  afterEach(() => {
    RedirectLatch.arm();
    http.verify();
    localStorage.clear();
  });

  function probe(): ApiResult | undefined {
    let answer: ApiResult | undefined;

    service.check().subscribe(result => (answer = result));
    http.expectOne('/api/account/get-user').flush({ code: 401 }, unauthorized);

    return answer;
  }

  async function loadUser(): Promise<UserModel> {
    const promise = users.user();
    http.expectOne('/api/account/get-user').flush({ code: 200, data: user }, ok);
    return promise;
  }

  async function rejectedLoad(): Promise<void> {
    const promise = users.user().catch(() => undefined);
    http.expectOne('/api/account/get-user').flush({ code: 401 }, unauthorized);
    await promise;
  }

  it('the probe is silent', () => {
    const message_handler = spyOn(TestBed.inject(MessageService), 'messageHandler');

    expect(probe()?.code).toBe(401);
    expect(message_handler).not.toHaveBeenCalled();
  });

  it('a rejected stored session is closed', async () => {
    LocalStorageService.set(['user', 'bearer'], { access_token: 'stored' });
    await loadUser();

    expect(probe()?.code).toBe(401);
    expect(bearer()).toBeNull();

    await loadUser();
  });

  it('an accepted session is kept', () => {
    LocalStorageService.set(['user', 'bearer'], { access_token: 'stored' });

    let answer: ApiResult | undefined;
    service.check().subscribe(result => (answer = result));

    const request = http.expectOne('/api/account/get-user');
    expect(request.request.headers.get('Authorization')).toBe('Bearer stored');
    request.flush({ code: 200, data: user }, ok);

    expect(answer?.status()).toBe(ResultStatus.success);
    expect(bearer()?.access_token).toBe('stored');
  });

  it('closing a session leaves the 401 latch alone', async () => {
    const navigate = spyOn(TestBed.inject(Router), 'navigateByUrl');
    LocalStorageService.set(['user', 'bearer'], { access_token: 'stored' });

    await rejectedLoad();
    expect(navigate).toHaveBeenCalledTimes(1);

    service.closeSession();
    await rejectedLoad();
    expect(navigate).toHaveBeenCalledTimes(1);

    service.logout();
    await rejectedLoad();
    expect(navigate).toHaveBeenCalledTimes(2);
  });
});
