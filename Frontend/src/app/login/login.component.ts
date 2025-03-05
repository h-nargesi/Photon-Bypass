import { NgStyle } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ButtonDirective,
  CardBodyComponent,
  CardComponent,
  CardGroupComponent,
  ColComponent,
  ContainerComponent,
  FormControlDirective,
  FormDirective,
  FormFeedbackComponent,
  GutterDirective,
  InputGroupComponent,
  InputGroupTextDirective,
  RowComponent,
  TextColorDirective,
} from '@coreui/angular';
import { cilLockLocked, cilUser } from '@coreui/icons';
import { IconDirective } from '@coreui/icons-angular';
import { AuthService, TranslationPipe } from '../@services';
import { result } from 'lodash-es';
import { ApiResult } from '../@models';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss'],
  imports: [
    FormsModule,
    NgStyle,
    ContainerComponent,
    RowComponent,
    ColComponent,
    CardGroupComponent,
    TextColorDirective,
    TranslationPipe,
    CardComponent,
    CardBodyComponent,
    FormDirective,
    InputGroupComponent,
    InputGroupTextDirective,
    IconDirective,
    FormControlDirective,
    FormFeedbackComponent,
    ButtonDirective,
    GutterDirective,
  ],
})
export class LoginComponent {
  icons = { cilUser, cilLockLocked };
  isValidated = false;
  username?: string;
  password?: string;
  result?: ApiResult;

  constructor(private readonly service: AuthService) {}

  login() {
    this.isValidated = true;

    if (!this.username || !this.password) return;

    this.service
      .login(this.username, this.password)
      .subscribe((result) => (this.result = result));
  }
}
