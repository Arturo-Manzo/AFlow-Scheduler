import { Component, DestroyRef, HostListener, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonDirective } from 'ui-design-system';
import {
  SmtpNotificationSettingsDto,
  TestSmtpNotificationRequest,
  UpdateSmtpNotificationSettingsRequest
} from '../../models/models';
import { NotificationSettingsService } from '../../services/notification-settings.service';

@Component({
  selector: 'app-notification-settings',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ButtonDirective],
  templateUrl: './notification-settings.component.html',
  styles: [`
    .smtp-hero {
      background:
        linear-gradient(140deg, rgba(1, 93, 181, 0.08), transparent 45%),
        linear-gradient(320deg, rgba(15, 122, 52, 0.08), transparent 52%),
        var(--bg-surface);
    }
    .smtp-hero-main {
      position: relative;
    }
    .smtp-hero-main::after {
      content: '';
      position: absolute;
      right: 1.25rem;
      top: 1.1rem;
      width: 92px;
      height: 92px;
      border: 1px solid color-mix(in srgb, var(--primary) 22%, white);
      background: linear-gradient(135deg, rgba(1, 93, 181, 0.10), rgba(255, 255, 255, 0.15));
      clip-path: polygon(0 0, 100% 0, 100% 62%, 62% 100%, 0 100%);
      pointer-events: none;
    }
    .smtp-kpi {
      background: linear-gradient(180deg, rgba(246, 248, 250, 0.92), rgba(255, 255, 255, 1));
    }
    .smtp-meta-grid {
      grid-template-columns: repeat(3, minmax(0, 1fr));
    }
    .smtp-accent-card {
      background: linear-gradient(180deg, var(--bg-surface), var(--bg-muted));
    }
    .smtp-panel {
      overflow: hidden;
    }
    .smtp-panel-header {
      background:
        linear-gradient(90deg, rgba(1, 93, 181, 0.08), transparent 35%),
        var(--bg-muted);
    }
    .smtp-panel-body {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .credential-note {
      padding: .8rem 1rem;
      border: 1px solid color-mix(in srgb, var(--warning) 28%, white);
      background: linear-gradient(90deg, rgba(154, 103, 0, 0.08), rgba(255, 248, 231, 0.9));
      color: var(--text-1);
      font-size: .87rem;
    }
    .smtp-form-grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: .85rem 1rem;
    }
    .smtp-state-toggle,
    .smtp-tls-toggle {
      padding: .75rem .85rem;
      border: 1px solid var(--border);
      background: var(--bg-muted);
      margin-bottom: 0;
    }
    .smtp-state-toggle {
      grid-column: 1 / -1;
    }
    .smtp-subhint {
      color: var(--text-3);
    }
    .smtp-test-shell {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .smtp-test-form {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto;
      gap: .85rem;
      align-items: end;
    }
    .smtp-test-field {
      margin-bottom: 0;
    }
    .smtp-test-actions {
      display: flex;
      align-items: end;
    }
    @media (max-width: 920px) {
      .smtp-meta-grid,
      .smtp-form-grid,
      .smtp-test-form {
        grid-template-columns: 1fr;
      }
      .smtp-test-actions {
        justify-content: flex-start;
      }
    }
  `]
})
export class NotificationSettingsComponent implements OnInit {
  private service = inject(NotificationSettingsService);
  private fb = inject(FormBuilder);
  private destroyRef = inject(DestroyRef);

  loading = signal(true);
  saving = signal(false);
  testing = signal(false);
  loadError = signal('');
  saveError = signal('');
  saveMessage = signal('');
  testError = signal('');
  testMessage = signal('');
  savedSettings = signal<SmtpNotificationSettingsDto | null>(null);

  smtpForm = this.fb.group({
    enabled: [false],
    host: ['', [Validators.maxLength(255)]],
    port: [587, [Validators.required, Validators.min(1), Validators.max(65535)]],
    username: ['', [Validators.maxLength(255)]],
    password: ['', [Validators.maxLength(255)]],
    fromAddress: ['', [Validators.email, Validators.maxLength(255)]],
    fromDisplayName: ['', [Validators.maxLength(255)]],
    enableSsl: [true]
  });

  testForm = this.fb.group({
    testRecipientEmail: ['', [Validators.required, Validators.email, Validators.maxLength(255)]]
  });

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.clearTransientMessages();
  }

  settingsLoaded(): boolean {
    return this.savedSettings() !== null;
  }

  canSendTest(): boolean {
    if (!this.settingsLoaded()) return false;
    if (this.smtpForm.dirty) return false;
    if (!this.savedSettings()?.enabled) return false;
    return this.testForm.valid;
  }

  getTestWarning(): string {
    if (!this.settingsLoaded()) return 'Load and save SMTP settings before using the test tool.';
    if (this.smtpForm.dirty) return 'You have unsaved configuration changes. Save them before sending a test email.';
    if (!this.savedSettings()?.enabled) return 'SMTP notifications must be enabled and saved before a live test can run.';
    return '';
  }

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.loadError.set('');
    this.clearTransientMessages();
    this.service.getSmtpSettings().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: settings => {
        this.savedSettings.set(settings);
        this.smtpForm.reset({
          enabled: settings.enabled,
          host: settings.host,
          port: settings.port,
          username: settings.username,
          password: '',
          fromAddress: settings.fromAddress,
          fromDisplayName: settings.fromDisplayName,
          enableSsl: settings.enableSsl
        });
        this.smtpForm.markAsPristine();
        this.loading.set(false);
      },
      error: err => {
        this.loadError.set(err?.error?.message || 'Failed to load SMTP settings.');
        this.loading.set(false);
      }
    });
  }

  saveSettings(): void {
    this.smtpForm.markAllAsTouched();
    this.clearTransientMessages();

    if (this.smtpForm.invalid || !this.validateEnabledFields()) {
      return;
    }

    const value = this.smtpForm.getRawValue();
    const request: UpdateSmtpNotificationSettingsRequest = {
      enabled: value.enabled ?? false,
      host: (value.host ?? '').trim(),
      port: Number(value.port ?? 587),
      username: (value.username ?? '').trim(),
      password: (value.password ?? '').trim() || undefined,
      fromAddress: (value.fromAddress ?? '').trim(),
      fromDisplayName: (value.fromDisplayName ?? '').trim(),
      enableSsl: value.enableSsl ?? true
    };

    this.saving.set(true);
    this.service.updateSmtpSettings(request).subscribe({
      next: settings => {
        this.savedSettings.set(settings);
        this.smtpForm.patchValue({ password: '' });
        this.smtpForm.markAsPristine();
        this.saveMessage.set('SMTP settings updated successfully.');
        this.saving.set(false);
      },
      error: err => {
        this.saveError.set(err?.error?.message || 'Failed to update SMTP settings.');
        this.saving.set(false);
      }
    });
  }

  sendTestEmail(): void {
    this.testForm.markAllAsTouched();
    this.testError.set('');
    this.testMessage.set('');

    if (!this.canSendTest()) {
      return;
    }

    const request: TestSmtpNotificationRequest = {
      testRecipientEmail: (this.testForm.value.testRecipientEmail ?? '').trim()
    };

    this.testing.set(true);
    this.service.sendSmtpTest(request).subscribe({
      next: result => {
        this.testMessage.set(`${result.message} Duration: ${result.durationMs} ms.`);
        this.testing.set(false);
      },
      error: err => {
        this.testError.set(err?.error?.message || 'Failed to send SMTP test email.');
        this.testing.set(false);
      }
    });
  }

  configFieldInvalid(field: 'host' | 'port' | 'fromAddress'): boolean {
    const control = this.smtpForm.get(field)!;
    const touched = control.touched || control.dirty;
    if (!touched) return false;

    if (field === 'host') {
      return !!this.smtpForm.value.enabled && !`${control.value ?? ''}`.trim();
    }

    if (field === 'fromAddress') {
      const value = `${control.value ?? ''}`.trim();
      return (!!this.smtpForm.value.enabled && !value) || control.hasError('email');
    }

    return control.invalid;
  }

  testFieldInvalid(): boolean {
    const control = this.testForm.get('testRecipientEmail')!;
    return control.invalid && (control.touched || control.dirty);
  }

  private validateEnabledFields(): boolean {
    if (!this.smtpForm.value.enabled) {
      return true;
    }

    const host = `${this.smtpForm.value.host ?? ''}`.trim();
    const fromAddress = `${this.smtpForm.value.fromAddress ?? ''}`.trim();
    const fromControl = this.smtpForm.get('fromAddress')!;

    if (!host || !fromAddress || fromControl.invalid) {
      this.saveError.set('Host and a valid sender address are required when SMTP notifications are enabled.');
      return false;
    }

    return true;
  }

  private clearTransientMessages(): void {
    this.saveError.set('');
    this.saveMessage.set('');
    this.testError.set('');
    this.testMessage.set('');
  }
}