import { Routes } from '@angular/router';
import { HomeComponent } from './home/home.component';
import { LoginComponent } from './login/login.component';
import { DashboardComponent } from './dashboard/dashboard.component';
import { DefaultLayoutComponent } from './default-layout';

export const routes: Routes = [
  {
    path: '',
    component: HomeComponent,
    pathMatch: 'full',
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
    ],
  },
  { path: '**', redirectTo: '' },
];
