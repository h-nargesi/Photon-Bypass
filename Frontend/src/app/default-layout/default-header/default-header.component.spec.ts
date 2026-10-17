import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { API_BASE_URL } from '../../@services/api-services/models/app-base-path';
import { DefaultHeaderComponent } from './default-header.component';

// smoke test (D6): the header renders once its user services resolve
describe('DefaultHeaderComponent', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<DefaultHeaderComponent>;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [DefaultHeaderComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([]),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(DefaultHeaderComponent);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  function answerUser(payload: Record<string, unknown>) {
    fixture.detectChanges();

    http.match(req => req.url === '/api/account/get-user').forEach(request =>
      request.flush(
        {
          code: 200,
          data: {
            targetArea: {
              main: { username: 'main', fullname: 'Main', email: 'm@t.l' },
            },
            ...payload,
          },
        },
        { status: 200, statusText: 'OK' },
      ),
    );

    return Promise.all([fixture.whenStable()]);
  }

  it('should create', async () => {
    await answerUser({
      username: 'main',
      fullname: 'Main',
      email: 'm@t.l',
      balance: 0,
      role: 0,
      allowSelfService: true,
    });

    expect(fixture.componentInstance).toBeTruthy();
  });

  it('a dependent account (D6) sees no financial links or wallet balance', async () => {
    await answerUser({
      username: 'main',
      fullname: 'Main',
      email: 'm@t.l',
      balance: 0,
      role: 0,
      allowSelfService: false,
    });
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;
    // only dashboard + history remain (renewal, payment and wallet balance gone)
    expect(element.querySelectorAll('a[cNavLink]').length).toBe(2);
    expect(element.querySelector('a[routerLink="/renewal"]')).toBeNull();
    expect(element.textContent).not.toContain('کیف پول:');
    expect(fixture.componentInstance.isSelfService).toBeFalse();
  });

  it('a manager sees the users area but not the admin pages', async () => {
    await answerUser({
      username: 'main',
      fullname: 'Main',
      email: 'm@t.l',
      balance: 0,
      role: 1,
      allowSelfService: true,
    });
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;
    expect(element.querySelector('a[routerLink="/users"]')).not.toBeNull();
    expect(element.querySelector('a[routerLink="/servers"]')).toBeNull();
    expect(element.querySelector('a[routerLink="/wallet"]')).toBeNull();
    expect(fixture.componentInstance.isManager).toBeTrue();
  });

  it('the admin sees users, servers and wallet, and the selector without sub users', async () => {
    await answerUser({
      username: 'boss',
      fullname: 'Boss',
      email: 'b@t.l',
      balance: 0,
      role: 2,
      allowSelfService: true,
    });
    fixture.detectChanges();

    const component = fixture.componentInstance;
    const element: HTMLElement = fixture.nativeElement;

    expect(component.isAdmin).toBeTrue();
    expect(element.querySelector('a[routerLink="/users"]')).not.toBeNull();
    expect(element.querySelector('a[routerLink="/servers"]')).not.toBeNull();
    expect(element.querySelector('a[routerLink="/wallet"]')).not.toBeNull();

    // the admin always reaches the user-list selector (docs/20 step 22)
    expect(component.canSelectTarget).toBeTrue();
  });
});
