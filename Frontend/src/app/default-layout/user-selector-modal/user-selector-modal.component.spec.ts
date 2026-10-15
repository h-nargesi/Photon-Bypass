import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { UserService } from '../../@services';
import { API_BASE_URL } from '../../@services/api-services/models/app-base-path';
import { UserSelectorModalComponent } from './user-selector-modal.component';

describe('UserSelectorModalComponent', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<UserSelectorModalComponent>;
  let component: UserSelectorModalComponent;
  let user_service: UserService;

  const user_payload = {
    username: 'main',
    fullname: 'Main User',
    email: 'm@t.l',
    balance: 0,
    targetArea: {
      main: { username: 'main', fullname: 'Main User', email: 'm@t.l' },
      sub1: { username: 'sub1', fullname: 'Sub One', email: 's@t.l' },
      other: { username: 'other', fullname: 'Other', email: 'o@t.l' },
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
    fixture = TestBed.createComponent(UserSelectorModalComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  async function loadUser() {
    const promise = user_service.user();
    http.expectOne('/api/account/get-user').flush(
      { code: 200, data: user_payload },
      { status: 200, statusText: 'OK' },
    );
    await promise;
    await fixture.whenStable();
  }

  it('lists every user of the target area', async () => {
    fixture.detectChanges();
    await loadUser();

    expect(component.current_user?.username).toBe('main');
    expect(component.usernameList).toEqual(['main', 'sub1', 'other']);
  });

  it('search filters the list by username and fullname', async () => {
    fixture.detectChanges();
    await loadUser();

    const input = fixture.nativeElement.querySelector('input');
    input.value = 'Sub';
    component.search();

    expect(component.usernameList).toEqual(['sub1']);

    input.value = 'main';
    component.search();

    expect(component.usernameList).toEqual(['main']);
  });

  it('selecting a user switches the UserService target and closes the modal', async () => {
    fixture.detectChanges();
    await loadUser();

    const emitted: (string | undefined)[] = [];
    user_service.onTargetChanged.subscribe(target => emitted.push(target?.username));

    component.visible = true;
    component.setTargetIndex('sub1');

    expect(user_service.targetName).toBe('sub1');
    expect(component.visible).toBeFalse();
    expect(emitted).toEqual(['sub1']);
  });
});
