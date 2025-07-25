import { ComponentFixture, TestBed } from '@angular/core/testing';

import {
  ButtonModule,
  CardModule,
  FormModule,
  GridModule,
} from '@coreui/angular';
import { IconModule, IconSetService } from '@coreui/icons-angular';
import { ICON_SUBSET } from '../@icons';
import { LoginComponent } from './login.component';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let iconSetService: IconSetService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [
        FormModule,
        CardModule,
        GridModule,
        ButtonModule,
        IconModule,
        LoginComponent,
      ],
      providers: [IconSetService],
    }).compileComponents();
  });

  beforeEach(() => {
    iconSetService = TestBed.inject(IconSetService);
    iconSetService.icons = { ...ICON_SUBSET };

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
