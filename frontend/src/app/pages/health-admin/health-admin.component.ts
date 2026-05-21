import { CommonModule } from '@angular/common';
import { Component, HostListener, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ButtonDirective } from 'ui-design-system';
import { Subscription, timer } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { ApplicationLogDto, ExecutionDto, HealthDashboardDto } from '../../models/models';
import { HealthService } from '../../services/health.service';
import { StatusBadgeComponent } from '../../components/status-badge/status-badge.component';
import { TranslatePipe } from '../../shared/translate.pipe';

@Component({
  selector: 'app-health-admin',
  standalone: true,
  imports: [CommonModule, StatusBadgeComponent, ButtonDirective, TranslatePipe],
  templateUrl: './health-admin.component.html',
  styles: [`
    .health-row { cursor: pointer; }
    .health-row:hover { background: var(--color-surface-low); }
    .truncate-cell { max-width: 18rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .detail-grid { display: grid; gap: .75rem; grid-template-columns: repeat(auto-fit, minmax(12rem, 1fr)); }
    .detail-term { color: var(--color-muted); font-size: .68rem; font-weight: 800; letter-spacing: .08em; text-transform: uppercase; }
    .detail-value { color: var(--color-text); font-size: .82rem; font-weight: 650; overflow-wrap: anywhere; }
    .log-message { max-height: 12rem; overflow: auto; white-space: pre-wrap; }
  `]
})
export class HealthAdminComponent implements OnInit, OnDestroy {
  private readonly healthService = inject(HealthService);
  private readonly router = inject(Router);
  private pollSub?: Subscription;

  dashboard = signal<HealthDashboardDto | null>(null);
  loading = signal(true);
  refreshing = signal(false);
  error = signal('');
  lastSyncedAt = signal<Date | null>(null);
  selectedLog = signal<ApplicationLogDto | null>(null);
  selectedExecution = signal<ExecutionDto | null>(null);

  ngOnInit(): void {
    this.reload(true);
    this.pollSub = timer(30000, 30000)
      .pipe(switchMap(() => this.healthService.getDashboard()))
      .subscribe({
        next: dashboard => this.applyDashboard(dashboard),
        error: () => void 0
      });
  }

  ngOnDestroy(): void {
    this.pollSub?.unsubscribe();
  }

  @HostListener('document:keydown.escape')
  closeOnEscape(): void {
    this.closeDetails();
  }

  reload(showLoading = false): void {
    if (showLoading && !this.dashboard()) {
      this.loading.set(true);
    } else {
      this.refreshing.set(true);
    }
    this.error.set('');

    this.healthService.getDashboard().subscribe({
      next: dashboard => {
        this.applyDashboard(dashboard);
        this.loading.set(false);
        this.refreshing.set(false);
      },
      error: () => {
        this.error.set('Unable to load health dashboard. Check API connectivity and authentication.');
        this.loading.set(false);
        this.refreshing.set(false);
      }
    });
  }

  private applyDashboard(dashboard: HealthDashboardDto): void {
    this.dashboard.set(dashboard);
    this.lastSyncedAt.set(new Date());
  }

  closeDetails(): void {
    this.selectedLog.set(null);
    this.selectedExecution.set(null);
  }

  openExecution(execution: ExecutionDto): void {
    if (execution.boxRunId != null) {
      void this.router.navigate(['/executions', execution.boxRunId]);
      return;
    }
    this.selectedExecution.set(execution);
  }

  formatTime(value?: string): string {
    if (!value) return '--';
    return new Date(value).toLocaleString();
  }

  formatDuration(ms?: number): string {
    if (ms == null) return '--';
    if (ms < 1000) return `${Math.round(ms)} ms`;
    return `${(ms / 1000).toFixed(2)} s`;
  }

  workerUtilization(): string {
    const status = this.dashboard()?.status;
    if (!status) return '--';
    return `${status.activeWorkers}/${status.totalWorkers}`;
  }

  recoveryState(): string {
    const status = this.dashboard()?.status;
    if (!status) return '--';
    return status.startupRecoveryCompleted ? 'Completed' : 'Pending';
  }

  statusBadgeClass(status?: string): string {
    const normalized = (status || '').toLowerCase();
    if (normalized === 'healthy') return 'badge badge-success';
    if (normalized === 'degraded') return 'badge badge-warning';
    if (normalized === 'unhealthy') return 'badge badge-danger';
    return 'badge badge-neutral';
  }

  levelBadgeClass(level?: string): string {
    const normalized = (level || '').toLowerCase();
    if (normalized === 'fatal' || normalized === 'error') return 'badge badge-danger';
    if (normalized === 'warning') return 'badge badge-warning';
    return 'badge badge-neutral';
  }

  apiStatus(): string {
    const dashboard = this.dashboard();
    if (!dashboard) return '--';
    return dashboard.status.apiOnline ? 'Online' : 'Offline';
  }

  dbStatus(): string {
    const dashboard = this.dashboard();
    if (!dashboard) return '--';
    return dashboard.summary.dbConnected ? 'Connected' : 'Disconnected';
  }
}
