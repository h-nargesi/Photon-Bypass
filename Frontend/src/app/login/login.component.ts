import { CommonModule, NgStyle } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  AlertComponent,
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
import { IconDirective } from '@coreui/icons-angular';
import { ICON_SUBSET } from '../@icons';
import { ApiResult } from '../@models';
import {
  AuthService,
  MessageService,
  TranslationPipe,
  UserService,
} from '../@services';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss'],
  imports: [
    CommonModule,
    FormsModule,
    NgStyle,
    RouterLink,
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
    AlertComponent,
  ],
})
export class LoginComponent {
  icons = ICON_SUBSET;
  isValidated = false;
  username?: string;
  password?: string;
  result?: ApiResult;

  constructor(
    private readonly service: AuthService,
    private readonly router: Router,
    active_route: ActivatedRoute,
    user_service: UserService
  ) {
    if (active_route.snapshot.routeConfig?.path === 'logout') {
      this.service.logout();
    } else {
      user_service.user().subscribe((user) => {
        console.log('login-check', user);
        if (user?.username) this.router.navigate(['dashboard']);
      });
    }
  }

  login() {
    this.isValidated = true;

    if (!this.username || !this.password) return;

    this.service.login(this.username, this.password).subscribe((result) => {
      this.result = result;

      setTimeout(() => this.router.navigate(['dashboard']), 1000);
    });
  }

  getColor(code: number) {
    return MessageService.getStatusCode(code);
  }
}
