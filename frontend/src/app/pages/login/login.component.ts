import { Component, signal, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { ApiResponse, LoginResponse } from '../../models/models';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="login-wrapper">
      <div class="login-card">
        <div class="login-logo">
          <span class="logo-icon">⏰</span>
          <h1>AScheduler</h1>
        </div>
        <p class="login-subtitle">Sign in to your account</p>

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>

          <div class="field">
            <label for="username">Username</label>
            <input
              id="username"
              formControlName="username"
              type="text"
              placeholder="Enter your username"
              autocomplete="username"
              [class.is-invalid]="isInvalid('username')"
            />
            @if (isInvalid('username')) {
              <span class="field-hint">Username is required.</span>
            }
          </div>

          <div class="field">
            <label for="password">Password</label>
            <input
              id="password"
              formControlName="password"
              type="password"
              placeholder="••••••••"
              autocomplete="current-password"
              [class.is-invalid]="isInvalid('password')"
            />
            @if (isInvalid('password')) {
              @if (form.get('password')!.hasError('required')) {
                <span class="field-hint">Password is required.</span>
              } @else if (form.get('password')!.hasError('minlength')) {
                <span class="field-hint">Password must be at least 6 characters.</span>
              }
            }
          </div>

          @if (error()) {
            <div class="alert alert-danger" role="alert">{{ error() }}</div>
          }

          <button
            type="submit"
            class="btn btn-primary btn-full btn-lg"
            [disabled]="loading() || form.invalid"
          >
            @if (loading()) {
              <span class="spinner"></span> Signing in…
            } @else {
              Sign in
            }
          </button>
        </form>
      </div>
    </div>
  `,
  styles: [`
    :host {
      flex: 1;
      display: flex;
      align-items: center;
      justify-content: center;
      background: var(--bg-app);
      padding: 1rem;
    }
    .login-wrapper {
      width: 100%;
      max-width: 380px;
    }
    .login-card {
      background: var(--bg-surface);
      padding: 2.25rem 2rem;
      border-radius: var(--radius-3);
      box-shadow: var(--shadow-2);
      border: 1px solid var(--border);
    }
    .login-logo {
      display: flex;
      align-items: center;
      gap: .5rem;
      margin-bottom: .375rem;
      .logo-icon { font-size: 1.5rem; line-height: 1; }
      h1 { font-size: 1.3rem; font-weight: 700; color: var(--text-1); margin: 0; }
    }
    .login-subtitle {
      font-size: .875rem;
      color: var(--text-3);
      margin-bottom: 1.75rem;
    }
    button[type="submit"] { margin-top: .5rem; }
  `]
})
export class LoginComponent {
  private fb     = inject(FormBuilder);
  private api    = inject(ApiService);
  private auth   = inject(AuthService);
  private router = inject(Router);

  form = this.fb.group({
    username: ['', [Validators.required]],
    password: ['', [Validators.required, Validators.minLength(6)]]
  });

  loading = signal(false);
  error   = signal('');

  isInvalid(field: string): boolean {
    const c = this.form.get(field)!;
    return c.invalid && (c.dirty || c.touched);
  }

  submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    this.loading.set(true);
    this.error.set('');

    const { username, password } = this.form.value;
    this.api.post<ApiResponse<LoginResponse>>('auth/login', { username, password })
      .subscribe({
        next: resp => {
          this.auth.storeLogin(resp.data);
          this.router.navigate(['/dashboard']);
        },
        error: () => {
          this.error.set('Invalid username or password. Please try again.');
          this.loading.set(false);
        }
      });
  }
}

