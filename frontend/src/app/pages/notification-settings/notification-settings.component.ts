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
import { TranslatePipe } from '../../shared/translate.pipe';

@Component({
  selector: 'app-notification-settings',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ButtonDirective, TranslatePipe],
  templateUrl: './notification-settings.component.html',
  styles: [`
    .smtp-summary {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto;
      gap: 1rem;
      align-items: center;
    }
    .smtp-summary-main {
      min-width: 0;
    }
    .smtp-summary-status,
    .smtp-summary-actions {
      display: flex;
      align-items: center;
      gap: .5rem;
      flex-wrap: wrap;
    }
    .smtp-panel {
      overflow: hidden;
    }
    .smtp-config-modal {
      width: 860px;
      max-width: min(96vw, 860px);
    }
    .smtp-modal-body {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .smtp-form-grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: .85rem 1rem;
    }
    .smtp-form-section {
      grid-column: 1 / -1;
      border-top: 1px solid var(--border);
      padding-top: 1rem;
    }
    .smtp-form-section--first {
      border-top: none;
      padding-top: 0;
    }
    .smtp-section-title {
      color: var(--text-3);
      font-size: .68rem;
      font-weight: 800;
      letter-spacing: .08em;
      text-transform: uppercase;
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
      color: var(--text-3) !important;
    }
    .smtp-modal-test {
      border-top: 1px solid var(--border);
      padding-top: 1rem;
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
      .smtp-summary {
        grid-template-columns: 1fr;
      }
      .smtp-form-grid,
      .smtp-test-form {
        grid-template-columns: 1fr;
      }
      .smtp-summary-actions {
        justify-content: flex-start;
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
  configOpen = signal(false);

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
    if (this.configOpen()) {
      this.closeConfig();
      return;
    }
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

  openConfig(): void {
    this.configOpen.set(true);
  }

  closeConfig(): void {
    this.configOpen.set(false);
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
