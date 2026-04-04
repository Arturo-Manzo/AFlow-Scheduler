import { Injectable, inject, signal, computed, OnDestroy } from '@angular/core';
import { ApiService } from './api.service';
import { ApiResponse } from '../models/models';

export interface SystemStatus {
  apiOnline: boolean;
  dbConnected: boolean;
  activeWorkers: number;
  totalWorkers: number;
  queueDepth: number;
  failNotificationEnabled: boolean;
  environment: string;
}

const APP_VERSION = '0.0.0';
const POLL_INTERVAL_MS = 30_000;

@Injectable({ providedIn: 'root' })
export class StatusService implements OnDestroy {
  private api = inject(ApiService);

  private _status = signal<SystemStatus | null>(null);
  private _lastSyncAt = signal<Date | null>(null);
  private _pollTimer: ReturnType<typeof setInterval> | null = null;

  readonly status = this._status.asReadonly();
  readonly lastSyncAt = this._lastSyncAt.asReadonly();
  readonly appVersion = APP_VERSION;

  readonly apiOnline = computed(() => this._status()?.apiOnline ?? false);
  readonly dbConnected = computed(() => this._status()?.dbConnected ?? false);
  readonly workers = computed(() => {
    const s = this._status();
    return s ? `${s.activeWorkers}/${s.totalWorkers} Workers` : '-- Workers';
  });
  readonly queueLabel = computed(() => {
    const s = this._status();
    return s ? `${s.queueDepth} in queue` : '-- in queue';
  });
  readonly failNotificationEnabled = computed(() => this._status()?.failNotificationEnabled ?? false);
  readonly environment = computed(() => this._status()?.environment ?? '--');
  readonly lastSyncLabel = computed(() => {
    const t = this._lastSyncAt();
    if (!t) return 'Never synced';
    const diffSec = Math.floor((Date.now() - t.getTime()) / 1000);
    if (diffSec < 5) return 'Just now';
    return `${diffSec}s ago`;
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
          queueDepth: 0,
          failNotificationEnabled: false,
          environment: '--'
        });
        this._lastSyncAt.set(new Date());
      }
    });
  }
}
