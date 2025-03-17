import { Routes } from '@angular/router';
import { DashboardComponent } from './dashboard/dashboard.component';
import { DefaultLayoutComponent } from './default-layout';
import { HistoryComponent } from './history/history.component';
import { HomeComponent } from './home/home.component';
import { LoginComponent } from './login/login.component';
import { RegisterComponent } from './register/register.component';

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
    ],
  },
  { path: '**', redirectTo: '' },
];
