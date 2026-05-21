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
import { NotificationSettingsComponent } from './pages/notification-settings/notification-settings.component';
import { HealthAdminComponent } from './pages/health-admin/health-admin.component';

export const routes: Routes = [
  { path: 'login',     component: LoginComponent,    canActivate: [loginGuard], title: 'Sign In | Chroniq' },
  { path: 'dashboard', component: DashboardComponent, canActivate: [authGuard], title: 'Dashboard | Chroniq' },
  { path: 'boxes',     component: TasksComponent,    canActivate: [authGuard], title: 'Boxes | Chroniq' },
  { path: 'boxes/:boxId/task/:taskId', component: TaskDetailComponent, canActivate: [authGuard], title: 'Task Detail | Chroniq' },
  { path: 'boxes/:boxId', component: BoxDetailComponent, canActivate: [authGuard], title: 'Box Detail | Chroniq' },
  { path: 'executions', component: BoxRunListComponent, canActivate: [authGuard], title: 'Executions | Chroniq' },
  { path: 'executions/:boxRunId', component: BoxRunDetailComponent, canActivate: [authGuard], title: 'Execution Detail | Chroniq' },
  { path: 'health', component: HealthAdminComponent, canActivate: [authGuard, adminGuard], title: 'Health | Chroniq' },
  { path: 'departments', component: DepartmentListComponent, canActivate: [authGuard, adminGuard], title: 'Departments | Chroniq' },
  { path: 'notification-settings', component: NotificationSettingsComponent, canActivate: [authGuard, adminGuard], title: 'SMTP Settings | Chroniq' },
  { path: 'users',     component: UsersComponent,    canActivate: [authGuard, adminGuard], title: 'Users | Chroniq' },
  { path: '',          redirectTo: 'login', pathMatch: 'full' },
  { path: '**',        redirectTo: 'login' }
];
