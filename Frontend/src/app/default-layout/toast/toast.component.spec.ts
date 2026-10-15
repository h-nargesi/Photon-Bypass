import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { AppToastComponent } from './toast.component';

// smoke test (D6): the toast body only needs animations to be available
describe('ToastComponent', () => {
  let component: AppToastComponent;
  let fixture: ComponentFixture<AppToastComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppToastComponent],
      providers: [provideNoopAnimations()],
    }).compileComponents();

    fixture = TestBed.createComponent(AppToastComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
