import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AppToastComponent } from './toast.component';

describe('ToastComponent', () => {
  let component: AppToastComponent;
  let fixture: ComponentFixture<AppToastComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppToastComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(AppToastComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
