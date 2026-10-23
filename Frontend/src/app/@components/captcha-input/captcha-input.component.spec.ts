import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { API_BASE_URL } from '../../@services/api-services/models/app-base-path';
import { CaptchaInputComponent } from './captcha-input.component';

describe('CaptchaInputComponent', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<CaptchaInputComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(CaptchaInputComponent);
  });

  afterEach(() => {
    http.verify();
  });

  function flushCaptcha(id: string) {
    http.expectOne(req => req.method === 'GET' && req.url === '/api/auth/captcha').flush(
      { code: 200, data: { id, image: `data:image/png;base64,${id}`, expiresInSeconds: 180 } },
      { status: 200, statusText: 'OK' },
    );
  }

  it('fetches an image on init and renders it once available', () => {
    const element: HTMLElement = fixture.nativeElement;

    fixture.detectChanges(); // ngOnInit -> fetch
    expect(element.querySelector('.captcha-box')).toBeNull(); // no image yet

    flushCaptcha('cap-1');
    fixture.detectChanges();

    const image = element.querySelector<HTMLImageElement>('.captcha-image');
    expect(image?.src).toBe('data:image/png;base64,cap-1');
  });

  it('is empty until a code is typed, then exposes the headers', () => {
    const component = fixture.componentInstance;
    fixture.detectChanges();
    flushCaptcha('cap-1');

    expect(component.empty).toBeTrue();
    expect(component.headers()).toBeUndefined();

    component.code = '  AB2CD ';

    expect(component.empty).toBeFalse();
    expect(component.headers()).toEqual({
      'X-Captcha-Id': 'cap-1',
      'X-Captcha-Code': 'AB2CD',
    });
  });

  it('refresh replaces the challenge (single-use contract)', () => {
    const component = fixture.componentInstance;
    fixture.detectChanges();
    flushCaptcha('cap-1');

    component.refresh();
    flushCaptcha('cap-2');

    component.code = 'AB2CD';
    expect(component.headers()).toEqual({
      'X-Captcha-Id': 'cap-2',
      'X-Captcha-Code': 'AB2CD',
    });
  });

  it('uses the page prefix for its placeholder', () => {
    const component = fixture.componentInstance;
    component.prefix = 'register';
    fixture.detectChanges();
    flushCaptcha('cap-1');
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('input.captcha-code-input');
    expect(input?.getAttribute('placeholder')).toBe('security code');
  });
});
