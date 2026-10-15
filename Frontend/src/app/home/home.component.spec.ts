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

    expect(fixture.componentInstance).toBeTruthy();
  });
});
