import { Component, HostListener, effect, inject, signal } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from './services/auth.service';
import { ApiService } from './services/api.service';
import { StatusService } from './services/status.service';
import { ApiResponse, ChangePasswordRequest } from './models/models';
import { Router } from '@angular/router';
import { ToastComponent } from './shared/toast.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, CommonModule, ReactiveFormsModule, ToastComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  public auth = inject(AuthService);
  private api = inject(ApiService);
  public statusService = inject(StatusService);
  private fb = inject(FormBuilder);
  private router = inject(Router);

  menuOpen = signal(false);
  showChangePassword = signal(false);
  passwordSaving = signal(false);
  passwordError = signal('');
  passwordSuccess = signal('');

  constructor() {
    effect(() => {
      if (this.auth.currentUser()) {
        this.statusService.startPolling();
      } else {
        this.statusService.stopPolling();
      }
    });
  }

  passwordForm = this.fb.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.minLength(8), Validators.pattern(/^(?=.*[A-Za-z])(?=.*\d).+$/)]],
    confirmPassword: ['', Validators.required]
  });

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.showChangePassword()) {
      this.closeChangePassword();
      return;
    }
    this.menuOpen.set(false);
  }

  toggleUserMenu(): void {
    this.menuOpen.update((value) => !value);
  }

  openChangePassword(): void {
    this.menuOpen.set(false);
    this.passwordForm.reset();
    this.passwordError.set('');
    this.passwordSuccess.set('');
    this.showChangePassword.set(true);
  }

  closeChangePassword(): void {
    this.showChangePassword.set(false);
    this.passwordSaving.set(false);
    this.passwordError.set('');
    this.passwordSuccess.set('');
  }

  passwordFieldInvalid(field: string): boolean {
    const control = this.passwordForm.get(field)!;
    return control.invalid && (control.dirty || control.touched);
  }

  confirmPasswordInvalid(): boolean {
    const confirm = this.passwordForm.get('confirmPassword')!;
    return !!confirm.value && confirm.value !== this.passwordForm.get('newPassword')!.value;
  }

  submitPasswordChange(): void {
    this.passwordForm.markAllAsTouched();
    this.passwordError.set('');
    this.passwordSuccess.set('');

    if (this.passwordForm.invalid || this.confirmPasswordInvalid()) {
      if (this.confirmPasswordInvalid()) {
        this.passwordError.set('New password and confirmation do not match.');
      }
      return;
    }

    const payload: ChangePasswordRequest = {
      currentPassword: this.passwordForm.value.currentPassword!,
      newPassword: this.passwordForm.value.newPassword!
    };

    this.passwordSaving.set(true);
    this.api.post<ApiResponse<object>>('users/change-password', payload).subscribe({
      next: () => {
        this.passwordSaving.set(false);
        this.passwordSuccess.set('Password updated successfully.');
        this.passwordForm.reset();
      },
      error: (err) => {
        this.passwordSaving.set(false);
        this.passwordError.set(err?.error?.message || 'Unable to change password.');
      }
    });
  }

  logout(): void {
    this.menuOpen.set(false);
    this.auth.logout();
  }

  currentSection(): string {
    const url = this.router.url;
    if (url.startsWith('/dashboard')) return 'Main Dashboard';
    if (url.match(/^\/(boxes|tasks)\/\d+\/task\/\d+/)) return 'Task Detail';
    if (url.match(/^\/(boxes|tasks)\/\d+$/)) return 'Box Detail';
    if (url.startsWith('/boxes') || url.startsWith('/tasks')) return 'Boxes And Tasks';
    if (url.startsWith('/executions')) return 'Execution Control';
    if (url.startsWith('/history')) return 'Execution History';
    if (url.startsWith('/departments')) return 'Department Management';
    if (url.startsWith('/notification-settings')) return 'SMTP Notification Settings';
    if (url.startsWith('/users')) return this.auth.isAdmin ? 'User Administration' : 'Account Center';
    return 'Control Panel';
  }
}
