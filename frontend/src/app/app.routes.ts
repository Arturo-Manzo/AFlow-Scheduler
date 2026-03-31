import { Routes } from '@angular/router';
import { authGuard }  from './guards/auth.guard';
import { loginGuard } from './guards/login.guard';
import { LoginComponent }    from './pages/login/login.component';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { TasksComponent }    from './pages/tasks/tasks.component';
import { LogsComponent }     from './pages/logs/logs.component';
import { UsersComponent }    from './pages/users/users.component';
import { BoxRunListComponent } from './pages/box-run-list/box-run-list.component';
import { BoxRunDetailComponent } from './pages/box-run-detail/box-run-detail.component';

export const routes: Routes = [
  { path: 'login',     component: LoginComponent,    canActivate: [loginGuard] },
  { path: 'dashboard', component: DashboardComponent, canActivate: [authGuard] },
  { path: 'tasks',     component: TasksComponent,    canActivate: [authGuard] },
  { path: 'executions', component: BoxRunListComponent, canActivate: [authGuard] },
  { path: 'executions/:boxRunId', component: BoxRunDetailComponent, canActivate: [authGuard] },
  { path: 'logs',      component: LogsComponent,     canActivate: [authGuard] },
  { path: 'users',     component: UsersComponent,    canActivate: [authGuard] },
  { path: '',          redirectTo: 'dashboard', pathMatch: 'full' },
  { path: '**',        redirectTo: 'login' }
];
