import { ComponentFixture, TestBed } from '@angular/core/testing';

import { UserSelectorModalComponent } from './user-selector-modal.component';

describe('UserSelectorModalComponent', () => {
  let component: UserSelectorModalComponent;
  let fixture: ComponentFixture<UserSelectorModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UserSelectorModalComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(UserSelectorModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
