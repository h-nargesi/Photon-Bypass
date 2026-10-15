import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { UserModel } from '../../@models';
import { API_BASE_URL } from '../api-services/models/app-base-path';
import { LocalStorageService } from '../local-storage/local-storage-service';
import { UserService } from './user-servcie';

describe('UserService', () => {
  let service: UserService;
  let http: HttpTestingController;

  const main_user: UserModel = {
    username: 'main',
    fullname: 'Main User',
    email: 'main@test.local',
    balance: 100,
    targetArea: {
      main: { username: 'main', fullname: 'Main User', email: 'main@test.local' },
      sub1: { username: 'sub1', fullname: 'Sub One', email: 'sub1@test.local' },
    },
  };

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
    service = TestBed.inject(UserService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  function loadUser(user: UserModel): Promise<UserModel> {
    const promise = service.user();
    const request = http.expectOne('/api/account/get-user');
    request.flush({ code: 200, data: user }, { status: 200, statusText: 'OK' });
    return promise;
  }

  it('loads the user from the account api', async () => {
    const user = await loadUser(main_user);

    expect(user.username).toBe('main');
    expect(service.hasSubUsers).toBeTrue();
  });

  it('caches the user and does not re-fetch until reload', async () => {
    await loadUser(main_user);

    const second = await service.user();

    expect(second.username).toBe('main');
    http.expectNone('/api/account/get-user');
  });

  it('first load without a stored target leaves targetUser unset until a name is given', async () => {
    await loadUser(main_user);

    // setTraget(undefined) hits the same-name early return while no target is set
    expect(service.targetUser).toBeUndefined();
    expect(service.hasSubUsers).toBeTrue();

    service.setTraget('main');

    expect(service.targetName).toBe('main');
    expect(LocalStorageService.get(['user', 'target'])).toBe('main');
  });

  it('setTraget switches the target, persists it and emits onTargetChanged', async () => {
    await loadUser(main_user);

    const emitted: (string | undefined)[] = [];
    service.onTargetChanged.subscribe(target => emitted.push(target?.username));

    service.setTraget('sub1');

    expect(service.targetUser?.username).toBe('sub1');
    expect(service.targetName).toBe('sub1');
    expect(LocalStorageService.get(['user', 'target'])).toBe('sub1');
    expect(emitted).toEqual(['sub1']);
  });

  it('setTraget with the same name does not re-emit', async () => {
    await loadUser(main_user);

    const emitted: (string | undefined)[] = [];
    service.onTargetChanged.subscribe(target => emitted.push(target?.username));

    service.setTraget('sub1');
    service.setTraget('sub1');

    expect(emitted).toEqual(['sub1']);
  });

  it('setTraget with an unknown name falls back to the account owner', async () => {
    await loadUser(main_user);

    service.setTraget('sub1');
    service.setTraget('who-is-this');

    expect(service.targetName).toBe('main');
    expect(LocalStorageService.get(['user', 'target'])).toBe('main');
  });

  it('without a target area the user load fails with the set-target error', async () => {
    const plain_user: UserModel = {
      username: 'solo',
      fullname: 'Solo User',
      email: 'solo@test.local',
      balance: 0,
    };

    await expectAsync(loadUser(plain_user))
      .toBeRejectedWith('can not set target to null!');
    expect(service.hasSubUsers).toBeFalse();
    expect(service.targetUser).toBeUndefined();
  });

  it('clear resets the cache so the next call fetches again', async () => {
    await loadUser(main_user);

    service.clear();

    const promise = service.user();
    http.expectOne('/api/account/get-user')
      .flush({ code: 200, data: main_user }, { status: 200, statusText: 'OK' });
    const user = await promise;

    expect(user.username).toBe('main');
  });
});
