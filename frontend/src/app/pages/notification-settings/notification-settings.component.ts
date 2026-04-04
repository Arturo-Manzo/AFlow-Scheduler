import { Component, DestroyRef, HostListener, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  SmtpNotificationSettingsDto,
  TestSmtpNotificationRequest,
  UpdateSmtpNotificationSettingsRequest
} from '../../models/models';
import { NotificationSettingsService } from '../../services/notification-settings.service';

@Component({
  selector: 'app-notification-settings',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="view-shell">
      <div class="view-hero smtp-hero">
        <div class="view-hero-main smtp-hero-main">
          <div class="view-eyebrow">Admin Delivery Control</div>
          <h1>SMTP Notification Settings</h1>
          <p class="view-description">
            Configure the mail relay used for task failure alerts, preserve credentials safely, and send a real validation email before relying on notifications in production.
          </p>
        </div>
        <div class="view-hero-kpi smtp-kpi">
          <span class="kpi-value">{{ loading() ? '--' : (settingsLoaded() ? (smtpForm.value.enabled ? 'ON' : 'OFF') : '--') }}</span>
          <span class="kpi-label">Relay State</span>
        </div>
        <div class="view-hero-kpi smtp-kpi">
          <span class="kpi-value">{{ loading() ? '--' : (savedSettings()?.hasPassword ? 'YES' : 'NO') }}</span>
          <span class="kpi-label">Stored Secret</span>
        </div>
        <div class="view-hero-kpi smtp-kpi">
          <span class="kpi-value">{{ loading() ? '--' : smtpForm.value.port }}</span>
          <span class="kpi-label">SMTP Port</span>
        </div>
      </div>

      <div class="meta-grid smtp-meta-grid">
        <div class="meta-card smtp-accent-card">
          <span class="meta-label">Effective Host</span>
          <span class="meta-value">{{ savedSettings()?.host || 'Not configured' }}</span>
        </div>
        <div class="meta-card smtp-accent-card">
          <span class="meta-label">Sender</span>
          <span class="meta-value">{{ savedSettings()?.fromAddress || 'Not configured' }}</span>
        </div>
        <div class="meta-card smtp-accent-card">
          <span class="meta-label">Transport Security</span>
          <span class="meta-value">{{ savedSettings()?.enableSsl ? 'TLS/SSL enabled' : 'Plain connection' }}</span>
        </div>
      </div>

      <section class="data-panel smtp-panel">
        <div class="panel-header smtp-panel-header">
          <div class="panel-title-wrap">
            <div class="panel-title">Relay Configuration</div>
            <div class="panel-subtitle">Only Administrators can edit these settings. Save changes before sending a test email.</div>
          </div>
          <div class="panel-toolbar">
            <button class="btn" (click)="reload()" [disabled]="loading() || saving() || testing()">Reload</button>
            <button class="btn btn-primary" (click)="saveSettings()" [disabled]="loading() || saving()">{{ saving() ? 'Saving...' : 'Save Settings' }}</button>
          </div>
        </div>

        @if (loading()) {
          <div class="loading-state"><span class="spinner"></span> Loading SMTP settings...</div>
        } @else {
          <div class="panel-body smtp-panel-body">
            @if (loadError()) {
              <div class="alert alert-danger">{{ loadError() }}</div>
            }

            @if (saveMessage()) {
              <div class="alert alert-success">{{ saveMessage() }}</div>
            }

            @if (saveError()) {
              <div class="alert alert-danger">{{ saveError() }}</div>
            }

            @if (savedSettings()?.hasPassword) {
              <div class="credential-note">
                <strong>Stored credential detected.</strong>
                Leave the password field blank to preserve the current secret, or enter a new one to rotate it.
              </div>
            }

            <form [formGroup]="smtpForm" novalidate class="smtp-form-grid">
              <div class="field field-check smtp-state-toggle">
                <input id="smtp-enabled" type="checkbox" formControlName="enabled" />
                <label for="smtp-enabled">Enable SMTP failure notifications</label>
              </div>

              <div class="field">
                <label for="smtp-host">Host</label>
                <input id="smtp-host" formControlName="host" placeholder="smtp-mail.outlook.com" [class.is-invalid]="configFieldInvalid('host')" />
                @if (configFieldInvalid('host')) {
                  <span class="field-hint">Host is required when notifications are enabled.</span>
                }
              </div>

              <div class="field">
                <label for="smtp-port">Port</label>
                <input id="smtp-port" type="number" formControlName="port" min="1" max="65535" [class.is-invalid]="configFieldInvalid('port')" />
                @if (configFieldInvalid('port')) {
                  <span class="field-hint">Port must be between 1 and 65535.</span>
                }
              </div>

              <div class="field">
                <label for="smtp-username">Username</label>
                <input id="smtp-username" formControlName="username" placeholder="mailer@company.com" />
              </div>

              <div class="field">
                <label for="smtp-password">Password</label>
                <input id="smtp-password" type="password" formControlName="password" placeholder="Leave blank to keep current password" />
                <span class="field-hint smtp-subhint">The password is never returned by the API after save.</span>
              </div>

              <div class="field">
                <label for="smtp-from-address">From Address</label>
                <input id="smtp-from-address" type="email" formControlName="fromAddress" placeholder="noreply@company.com" [class.is-invalid]="configFieldInvalid('fromAddress')" />
                @if (configFieldInvalid('fromAddress')) {
                  <span class="field-hint">A valid sender address is required when notifications are enabled.</span>
                }
              </div>

              <div class="field">
                <label for="smtp-from-name">From Display Name</label>
                <input id="smtp-from-name" formControlName="fromDisplayName" placeholder="AScheduler Notifications" />
              </div>

              <div class="field field-check smtp-tls-toggle">
                <input id="smtp-ssl" type="checkbox" formControlName="enableSsl" />
                <label for="smtp-ssl">Use TLS / SSL</label>
              </div>
            </form>
          </div>
        }
      </section>

      <section class="data-panel smtp-panel">
        <div class="panel-header smtp-panel-header">
          <div class="panel-title-wrap">
            <div class="panel-title">Live Test</div>
            <div class="panel-subtitle">This sends a real email using the currently saved settings stored in the backend.</div>
          </div>
        </div>

        <div class="panel-body smtp-test-shell">
          @if (getTestWarning()) {
            <div class="alert alert-warning">{{ getTestWarning() }}</div>
          }

          @if (testMessage()) {
            <div class="alert alert-success">{{ testMessage() }}</div>
          }

          @if (testError()) {
            <div class="alert alert-danger">{{ testError() }}</div>
          }

          <form [formGroup]="testForm" (ngSubmit)="sendTestEmail()" novalidate class="smtp-test-form">
            <div class="field smtp-test-field">
              <label for="smtp-test-email">Test Recipient Email</label>
              <input id="smtp-test-email" type="email" formControlName="testRecipientEmail" placeholder="admin@company.com" [class.is-invalid]="testFieldInvalid()" />
              @if (testFieldInvalid()) {
                <span class="field-hint">Enter a valid email address for the test send.</span>
              }
            </div>
            <div class="smtp-test-actions">
              <button class="btn btn-primary" type="submit" [disabled]="testing() || !canSendTest()">{{ testing() ? 'Sending...' : 'Send Test Email' }}</button>
            </div>
          </form>
        </div>
      </section>
    </div>
  `,
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