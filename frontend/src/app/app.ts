import { Component, HostListener, HostBinding, effect, inject, signal, Inject } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from './services/auth.service';
import { ApiService } from './services/api.service';
import { StatusService } from './services/status.service';
import { ApiResponse, ChangePasswordRequest } from './models/models';
import { NavigationEnd, Router } from '@angular/router';
import { ToastComponent } from './shared/toast.component';
import { ThemeService } from 'ui-design-system';
import { ButtonDirective } from 'ui-design-system';
import { PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { LanguageService, AppLanguage } from './services/language.service';
import { TranslatePipe } from './shared/translate.pipe';
import { Title } from '@angular/platform-browser';
import { filter } from 'rxjs';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, CommonModule, ReactiveFormsModule, ToastComponent, ButtonDirective, TranslatePipe],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  @HostBinding('class.authenticated')
  get isAuthenticated(): boolean {
    return !!this.auth.currentUser();
  }

  public auth = inject(AuthService);
  private api = inject(ApiService);
  public statusService = inject(StatusService);
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private themeService = inject(ThemeService);
  private title = inject(Title);
  public i18n = inject(LanguageService);

  constructor(@Inject(PLATFORM_ID) private readonly platformId: object) {
    if (isPlatformBrowser(this.platformId)) {
      this.themeService.init();
    }
    effect(() => {
      if (this.auth.currentUser()) {
        this.statusService.startPolling();
      } else {
        this.statusService.stopPolling();
      }
    });
    effect(() => {
      this.i18n.language();
      this.updateDocumentTitle();
    });
    this.router.events.pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd)).subscribe(() => {
      this.updateDocumentTitle();
    });
  }

  menuOpen = signal(false);
  sidebarCollapsed = signal(false);
  showChangePassword = signal(false);
  passwordSaving = signal(false);
  passwordError = signal('');
  passwordSuccess = signal('');

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

  toggleSidebar(): void {
    this.sidebarCollapsed.update((value) => !value);
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
        this.passwordError.set(this.i18n.t('New password and confirmation do not match.'));
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
        this.passwordSuccess.set(this.i18n.t('Password updated successfully.'));
        this.passwordForm.reset();
      },
      error: (err) => {
        this.passwordSaving.set(false);
        this.passwordError.set(err?.error?.message || this.i18n.t('Unable to change password.'));
      }
    });
  }

  setLanguage(language: AppLanguage): void {
    this.i18n.setLanguage(language);
  }

  logout(): void {
    this.menuOpen.set(false);
    this.auth.logout();
  }

  currentSection(): string {
    const url = this.router.url;
    if (url.startsWith('/login')) return this.i18n.t('Sign In');
    if (url.startsWith('/dashboard')) return this.i18n.t('Main Dashboard');
    if (url.match(/^\/(boxes|tasks)\/\d+\/task\/\d+/)) return this.i18n.t('Task Detail');
    if (url.match(/^\/(boxes|tasks)\/\d+$/)) return this.i18n.t('Box Detail');
    if (url.startsWith('/boxes') || url.startsWith('/tasks')) return this.i18n.t('Boxes And Tasks');
    if (url.startsWith('/executions')) return this.i18n.t('Execution Control');
    if (url.startsWith('/health')) return this.i18n.t('System Health');
    if (url.startsWith('/departments')) return this.i18n.t('Department Management');
    if (url.startsWith('/notification-settings')) return this.i18n.t('SMTP Notification Settings');
    if (url.startsWith('/users')) return this.auth.isAdmin ? this.i18n.t('User Administration') : this.i18n.t('Account Center');
    return this.i18n.t('Control Panel');
  }

  private updateDocumentTitle(): void {
    this.title.setTitle(`${this.currentSection()} | Chroniq`);
  }
}
