import { Injectable, inject, signal, computed, OnDestroy } from '@angular/core';
import { ApiService } from './api.service';
import { ApiResponse, SystemStatus } from '../models/models';
import packageJson from '../../../package.json';
import { LanguageService } from './language.service';

const POLL_INTERVAL_MS = 30_000;

@Injectable({ providedIn: 'root' })
export class StatusService implements OnDestroy {
  private api = inject(ApiService);
  private language = inject(LanguageService);

  private _status = signal<SystemStatus | null>(null);
  private _lastSyncAt = signal<Date | null>(null);
  private _pollTimer: ReturnType<typeof setInterval> | null = null;

  readonly status = this._status.asReadonly();
  readonly lastSyncAt = this._lastSyncAt.asReadonly();
  readonly frontendVersion = packageJson.version ?? 'unknown';
  readonly backendVersion = computed(() => this._status()?.backendVersion ?? 'unknown');

  readonly apiOnline = computed(() => this._status()?.apiOnline ?? false);
  readonly dbConnected = computed(() => this._status()?.dbConnected ?? false);
  readonly workers = computed(() => {
    const s = this._status();
    return s
      ? this.language.t('{active}/{total} Workers', { active: s.activeWorkers, total: s.totalWorkers })
      : this.language.t('-- Workers');
  });
  readonly runningBoxRuns = computed(() => this._status()?.runningBoxRuns ?? 0);
  readonly runningExecutions = computed(() => this._status()?.runningExecutions ?? 0);
  readonly staleExecutions = computed(() => this._status()?.staleExecutions ?? 0);
  readonly staleExecutionThresholdMinutes = computed(() => this._status()?.staleExecutionThresholdMinutes ?? 0);
  readonly hasStaleExecutions = computed(() => this.staleExecutions() > 0);
  readonly queueLabel = computed(() => {
    const s = this._status();
    return s ? this.language.t('{count} in queue', { count: s.queueDepth }) : this.language.t('-- in queue');
  });
  readonly staleLabel = computed(() => {
    const s = this._status();
    if (!s) return this.language.t('-- stale');
    return this.language.t('{count} stale > {minutes}m', {
      count: s.staleExecutions,
      minutes: s.staleExecutionThresholdMinutes
    });
  });
  readonly failNotificationEnabled = computed(() => this._status()?.failNotificationEnabled ?? false);
  readonly startupRecoveryCompleted = computed(() => this._status()?.startupRecoveryCompleted ?? false);
  readonly recoveryLabel = computed(() => {
    const s = this._status();
    if (!s) return this.language.t('Recovery: --');
    if (!s.autoRecoveryEnabled) return this.language.t('Recovery: Disabled');
    if (!s.startupRecoveryCompleted) return this.language.t('Recovery: Pending');

    const completedAt = s.lastRecoveryCompletedAtUtc
      ? new Date(s.lastRecoveryCompletedAtUtc).toLocaleTimeString()
      : '--';

    return this.language.t('Recovery: {execCount} exec / {runCount} runs @ {time}', {
      execCount: s.lastRecoveredExecutionCount,
      runCount: s.lastRecoveredBoxRunCount,
      time: completedAt
    });
  });
  readonly environment = computed(() => this._status()?.environment ?? '--');
  readonly lastSyncLabel = computed(() => {
    const t = this._lastSyncAt();
    if (!t) return this.language.t('Never synced');
    const diffSec = Math.floor((Date.now() - t.getTime()) / 1000);
    if (diffSec < 5) return this.language.t('Just now');
    return this.language.t('{seconds}s ago', { seconds: diffSec });
  });

  startPolling(): void {
    this.fetchStatus();
    if (!this._pollTimer) {
      this._pollTimer = setInterval(() => this.fetchStatus(), POLL_INTERVAL_MS);
    }
  }

  stopPolling(): void {
    if (this._pollTimer !== null) {
      clearInterval(this._pollTimer);
      this._pollTimer = null;
    }
  }

  ngOnDestroy(): void {
    this.stopPolling();
  }

  private fetchStatus(): void {
    this.api.get<ApiResponse<SystemStatus>>('status').subscribe({
      next: (res) => {
        if (res.success) {
          this._status.set(res.data);
          this._lastSyncAt.set(new Date());
        }
      },
      error: () => {
        this._status.set({
          apiOnline: false,
          dbConnected: false,
          activeWorkers: 0,
          totalWorkers: 0,
          runningBoxRuns: 0,
          runningExecutions: 0,
          staleExecutions: 0,
          staleExecutionThresholdMinutes: 0,
          queueDepth: 0,
          failNotificationEnabled: false,
          backendVersion: 'unknown',
          autoRecoveryEnabled: true,
          startupRecoveryCompleted: false,
          lastRecoveryCompletedAtUtc: undefined,
          lastRecoveredExecutionCount: 0,
          lastRecoveredBoxRunCount: 0,
          environment: '--'
        });
        this._lastSyncAt.set(new Date());
      }
    });
  }
}
