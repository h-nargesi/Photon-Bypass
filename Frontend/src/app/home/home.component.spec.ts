import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { API_BASE_URL } from '../@services/api-services/models/app-base-path';
import { HomeComponent } from './home.component';

// smoke test (D6): the home page only lists the visible prices
describe('HomeComponent', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<HomeComponent>;

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
    fixture = TestBed.createComponent(HomeComponent);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('should create', async () => {
    fixture.detectChanges();

    http.expectOne('/api/basics/prices').flush(
      { code: 200, data: [] },
      { status: 200, statusText: 'OK' },
    );
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.componentInstance).toBeTruthy();
    expect(fixture.nativeElement.querySelector('c-alert')).toBeNull();
  });

  it('a failed price load is shown on the page', async () => {
    async function expectAlert(
      failed: ComponentFixture<HomeComponent>,
      body: object,
      response: { status: number; statusText: string },
    ) {
      failed.detectChanges();
      http.expectOne('/api/basics/prices').flush(body, response);
      await failed.whenStable();
      failed.detectChanges();

      expect(failed.nativeElement.querySelector('c-alert')?.textContent)
        .toContain('خطا در تبادل داده.');
    }

    await expectAlert(fixture, { code: 500 }, { status: 200, statusText: 'OK' });
    await expectAlert(
      TestBed.createComponent(HomeComponent),
      { code: 503 },
      { status: 503, statusText: 'Service Unavailable' },
    );
  });
});
