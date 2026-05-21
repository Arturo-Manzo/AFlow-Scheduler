import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonDirective } from 'ui-design-system';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { ApiResponse, LoginResponse } from '../../models/models';
import { LanguageService } from '../../services/language.service';
import { TranslatePipe } from '../../shared/translate.pipe';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, ButtonDirective, TranslatePipe],
  templateUrl: './login.component.html',
  styles: [`
    .ui-auth-layout__background {
      position: absolute;
      inset: 0;
      pointer-events: none;
      background: radial-gradient(circle at 20% 20%, color-mix(in srgb, var(--color-accent) 14%, transparent) 36%, transparent),
                  radial-gradient(circle at 80% 0%, color-mix(in srgb, var(--color-accent) 10%, transparent) 30%, transparent);
    }
  `]
})
export class LoginComponent {
  private static readonly USERNAME_HINT = 'Username hint';
  private static readonly PASSWORD_HINT = 'Password hint';

  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly i18n = inject(LanguageService);

  readonly isLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly usernamePlaceholder = signal(this.i18n.t(LoginComponent.USERNAME_HINT));
  readonly passwordPlaceholder = signal(this.i18n.t(LoginComponent.PASSWORD_HINT));

  readonly form = this.fb.nonNullable.group({
    username: ['', [Validators.required]],
    password: ['', [Validators.required]],
  });

  clearPlaceholderOnFocus(field: 'username' | 'password'): void {
    if (field === 'username') {
      this.usernamePlaceholder.set('');
      return;
    }
    this.passwordPlaceholder.set('');
  }

  restorePlaceholderOnBlur(field: 'username' | 'password'): void {
    if (field === 'username' && !this.form.controls.username.value) {
      this.usernamePlaceholder.set(this.i18n.t(LoginComponent.USERNAME_HINT));
      return;
    }
    if (field === 'password' && !this.form.controls.password.value) {
      this.passwordPlaceholder.set(this.i18n.t(LoginComponent.PASSWORD_HINT));
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    const { username, password } = this.form.getRawValue();
    this.api.post<ApiResponse<LoginResponse>>('auth/login', { username, password }).subscribe({
      next: resp => {
        this.isLoading.set(false);
        this.auth.storeLogin(resp.data);
        this.router.navigateByUrl('/dashboard');
      },
      error: () => {
        this.isLoading.set(false);
        this.errorMessage.set(this.i18n.t('Invalid credentials. Check username and password.'));
      },
    });
  }

}
