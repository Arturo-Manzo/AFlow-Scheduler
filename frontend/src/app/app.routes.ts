import { Routes } from '@angular/router';
import { authGuard }  from './guards/auth.guard';
import { adminGuard } from './guards/admin.guard';
import { loginGuard } from './guards/login.guard';
import { LoginComponent }    from './pages/login/login.component';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { TasksComponent }    from './pages/tasks/tasks.component';
import { BoxDetailComponent } from './pages/box-detail/box-detail.component';
import { TaskDetailComponent } from './pages/task-detail/task-detail.component';
import { UsersComponent }    from './pages/users/users.component';
import { BoxRunListComponent } from './pages/box-run-list/box-run-list.component';
import { BoxRunDetailComponent } from './pages/box-run-detail/box-run-detail.component';
import { DepartmentListComponent } from './components/department-list/department-list.component';
import { ExecutionHistoryComponent } from './pages/execution-history/execution-history.component';
import { NotificationSettingsComponent } from './pages/notification-settings/notification-settings.component';

export const routes: Routes = [
  { path: 'login',     component: LoginComponent,    canActivate: [loginGuard] },
  { path: 'dashboard', component: DashboardComponent, canActivate: [authGuard] },
  { path: 'boxes',     component: TasksComponent,    canActivate: [authGuard] },
  { path: 'boxes/:boxId/task/:taskId', component: TaskDetailComponent, canActivate: [authGuard] },
  { path: 'boxes/:boxId', component: BoxDetailComponent, canActivate: [authGuard] },
  { path: 'executions', component: BoxRunListComponent, canActivate: [authGuard] },
  { path: 'executions/:boxRunId', component: BoxRunDetailComponent, canActivate: [authGuard] },
  { path: 'departments', component: DepartmentListComponent, canActivate: [authGuard, adminGuard] },
  { path: 'notification-settings', component: NotificationSettingsComponent, canActivate: [authGuard, adminGuard] },
  { path: 'history',   component: ExecutionHistoryComponent, canActivate: [authGuard] },
  { path: 'failed',    redirectTo: 'history', pathMatch: 'full' },
  { path: 'logs',      redirectTo: 'history', pathMatch: 'full' },
  { path: 'users',     component: UsersComponent,    canActivate: [authGuard, adminGuard] },
  { path: '',          redirectTo: 'dashboard', pathMatch: 'full' },
  { path: '**',        redirectTo: 'login' }
];
