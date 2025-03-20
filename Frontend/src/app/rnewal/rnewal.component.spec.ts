import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RnewalComponent } from './rnewal.component';

describe('RnewalComponent', () => {
  let component: RnewalComponent;
  let fixture: ComponentFixture<RnewalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RnewalComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(RnewalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
