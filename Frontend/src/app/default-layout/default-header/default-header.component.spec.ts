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

  it('should create', async () => {
    const pending: Promise<unknown>[] = [];

    // answer any user lookups triggered during detection
    const original = fixture.componentInstance;
    void original;

    fixture.detectChanges();

    http.match(req => req.url === '/api/account/get-user').forEach(request => {
      pending.push(
        Promise.resolve(
          request.flush(
            {
              code: 200,
              data: {
                username: 'main',
                fullname: 'Main',
                email: 'm@t.l',
                balance: 0,
                targetArea: { main: { username: 'main', fullname: 'Main', email: 'm@t.l' } },
              },
            },
            { status: 200, statusText: 'OK' },
          ),
        ),
      );
    });

    await Promise.all(pending);
    await fixture.whenStable();

    expect(fixture.componentInstance).toBeTruthy();
  });
});
