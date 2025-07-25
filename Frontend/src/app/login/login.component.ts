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
import { ApiResult, ResultStatus } from '../@models';
import { AuthService, MessageService, TranslationPipe } from '../@services';

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
  readonly icons = ICON_SUBSET;
  readonly redirect: string;
  isValidated = false;
  username?: string;
  password?: string;
  result?: ApiResult;

  constructor(
    private readonly service: AuthService,
    private readonly router: Router,
    active_route: ActivatedRoute,
    auth_service: AuthService
  ) {
    this.redirect = active_route.snapshot.queryParams['redirect'] ?? '/dashboard';

    if (active_route.snapshot.routeConfig?.path === 'logout') {
      this.service.logout();
    } else {
      auth_service.check().subscribe((result) => {
        if (result?.status() === ResultStatus.success) {
          console.log('router', this.router);
          this.router.navigateByUrl(this.redirect);
        }
      });
    }
  }

  login() {
    this.isValidated = true;

    if (!this.username || !this.password) return;

    this.service.login(this.username, this.password).subscribe((result) => {
      this.result = result;
      
      if (result.status() === ResultStatus.success) {
        setTimeout(() => this.router.navigateByUrl(this.redirect), 1000);
      }
    });
  }

  getColor(code: ResultStatus) {
    return MessageService.getStatusCode(code);
  }
}
