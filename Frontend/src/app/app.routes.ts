import { Routes } from '@angular/router';
import { ChangePasswordComponent } from './change-password/change-password.component';
import { DashboardComponent } from './dashboard/dashboard.component';
import { DefaultLayoutComponent } from './default-layout';
import { ForgotPasswordComponent } from './forgot-password/forgot-password.component';
import { HistoryComponent } from './history/history.component';
import { HomeComponent } from './home/home.component';
import { LoginComponent } from './login/login.component';
import { PaymentComponent } from './payment/payment.component';
import { RegisterComponent } from './register/register.component';
import { RenewalComponent } from './renewal/renewal.component';
import {
  adminGuard,
  authGuard,
  managerGuard,
  selfServiceGuard,
} from './@services';
import { ServersComponent } from './management/servers/servers.component';
import { UsersComponent } from './management/users/users.component';
import { WalletComponent } from './management/wallet/wallet.component';

export const routes: Routes = [
  {
    path: '',
    component: HomeComponent,
    pathMatch: 'full',
  },
  {
    path: 'register',
    component: RegisterComponent,
  },
  {
    path: 'login',
    component: LoginComponent,
  },
  {
    path: 'logout',
    component: LoginComponent,
  },
  {
    path: '',
    component: DefaultLayoutComponent,
    canActivate: [authGuard],
    children: [
      {
        path: 'dashboard',
        component: DashboardComponent,
      },
      {
        path: 'history',
        component: HistoryComponent,
      },
      {
        path: 'edit-user-info',
        component: RegisterComponent,
      },
      {
        path: 'edit-account-info',
        component: RegisterComponent,
      },
      {
        path: 'change-password',
        component: ChangePasswordComponent,
      },
      {
        path: 'change-ovpn-password',
        component: ChangePasswordComponent,
      },
      {
        path: 'rotate-ovpn-id',
        component: ChangePasswordComponent,
      },
      {
        path: 'renewal',
        component: RenewalComponent,
        canActivate: [selfServiceGuard],
      },
      {
        path: 'payment',
        component: PaymentComponent,
        canActivate: [selfServiceGuard],
      },
      {
        path: 'users',
        component: UsersComponent,
        canActivate: [managerGuard],
      },
      {
        path: 'servers',
        component: ServersComponent,
        canActivate: [adminGuard],
      },
      {
        path: 'wallet',
        component: WalletComponent,
        canActivate: [adminGuard],
      },
    ],
  },
  {
    path: 'reset-password',
    component: ChangePasswordComponent,
  },
  {
    path: 'forgot-password',
    component: ForgotPasswordComponent,
  },
  { path: '**', redirectTo: '' },
];
