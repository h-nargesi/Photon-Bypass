import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { ToasterComponent } from '@coreui/angular';
import { ResultStatus } from '../@models';
import { API_BASE_URL } from '../@services/api-services/models/app-base-path';
import { MessageService } from '../@services/message-handler/message-service';
import { DefaultLayoutComponent } from './default-layout.component';

describe('DefaultLayoutComponent', () => {
  let http: HttpTestingController;
  let messages: MessageService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [DefaultLayoutComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([]),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    messages = TestBed.inject(MessageService);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  function mount(): ComponentFixture<DefaultLayoutComponent> {
    const fixture = TestBed.createComponent(DefaultLayoutComponent);

    fixture.detectChanges();
    http.match(req => req.url === '/api/account/get-user').forEach(request =>
      request.flush(
        {
          code: 200,
          data: {
            username: 'main',
            targetArea: {
              main: { username: 'main', fullname: 'Main', email: 'm@t.l' },
            },
          },
        },
        { status: 200, statusText: 'OK' },
      ),
    );

    return fixture;
  }

  it('registers its toaster once the view is initialised', () => {
    const fixture = mount();

    messages.messageHandler(ResultStatus.success, 'title', 'body');

    expect(fixture.nativeElement.querySelector('c-toaster app-toast')).not.toBeNull();
  });

  it('a destroyed layout is never used again', () => {
    const first = mount();
    const dead_toaster = first.debugElement.query(By.directive(ToasterComponent))
      .componentInstance as ToasterComponent;
    const dead_addToast = spyOn(dead_toaster, 'addToast');

    first.destroy();

    messages.messageHandler(ResultStatus.error, 'title', 'body');
    expect(dead_addToast).not.toHaveBeenCalled();

    const second = mount();
    messages.messageHandler(ResultStatus.success, 'title', 'body');

    expect(dead_addToast).not.toHaveBeenCalled();
    expect(second.nativeElement.querySelector('c-toaster app-toast')).not.toBeNull();
    expect(first.nativeElement.querySelector('c-toaster app-toast')).toBeNull();
  });
});
