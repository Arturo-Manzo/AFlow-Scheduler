import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription, interval } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { BoxRunDto, BoxRunStatus, BoxRunMetricsDto, BoxRunTaskExecutionDto, TaskExecutionLogDto } from '../../models/models';
import { ExecutionService } from '../../services/execution.service';
import { StatusBadgeComponent } from '../../components/status-badge/status-badge.component';
import { TaskTableComponent } from '../../components/task-table/task-table.component';
import { ErrorModalComponent } from '../../components/error-modal/error-modal.component';
import { ConfirmModalComponent } from '../../components/confirm-modal/confirm-modal.component';
import { TaskLogsModalComponent } from '../../components/task-logs-modal/task-logs-modal.component';

@Component({
  selector: 'app-box-run-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, StatusBadgeComponent, TaskTableComponent, ErrorModalComponent, ConfirmModalComponent, TaskLogsModalComponent],
  template: `
    <div class="view-shell">
      <div class="page-header">
        <div>
          <a class="back-link" routerLink="/executions">← Back to executions</a>
          <h1>BoxRun #{{ boxRunId() }}</h1>
          <div class="page-subtitle">Detailed operational view with task breakdown, logs and aggregate metrics.</div>
        </div>
        <div class="page-actions">
          <button class="btn" (click)="reload()">Refresh</button>
        </div>
      </div>

      @if (loading()) {
        <div class="loading-state"><span class="spinner"></span> Loading BoxRun details...</div>
      } @else if (error()) {
        <div class="alert alert-danger">{{ error() }}</div>
      } @else if (run()) {
      <div class="meta-grid">
        <div class="meta-card">
          <span class="meta-label">BoxRun ID</span>
          <span class="meta-value">{{ run()!.id }}</span>
        </div>
        <div class="meta-card">
          <span class="meta-label">Status</span>
          <span class="meta-value"><app-status-badge [status]="displayStatus()" /></span>
        </div>
        <div class="meta-card">
          <span class="meta-label">Trigger</span>
          <span class="meta-value">{{ run()!.triggerSource }}</span>
        </div>
        <div class="meta-card">
          <span class="meta-label">Scheduled For</span>
          <span class="meta-value">{{ formatTime(run()!.scheduledForUtc) }}</span>
        </div>
        <div class="meta-card">
          <span class="meta-label">Start Time</span>
          <span class="meta-value">{{ formatTime(run()!.startTime) }}</span>
        </div>
        <div class="meta-card">
          <span class="meta-label">End Time</span>
          <span class="meta-value">{{ formatTime(run()!.endTime) }}</span>
        </div>
        <div class="meta-card">
          <span class="meta-label">Duration</span>
          <span class="meta-value">{{ run()!.durationSeconds != null ? run()!.durationSeconds + 's' : '--' }}</span>
        </div>
        <div class="meta-card">
          <span class="meta-label">Progress</span>
          <span class="meta-value">{{ completedTasks() }} / {{ tasks().length }}</span>
        </div>
      </div>

      @if (metrics()) {
        <section class="metrics-panel">
          <div class="metrics-header">
            <h2 class="section-title" style="margin:0">Execution Metrics</h2>
            <div class="metrics-rate">{{ metrics()!.successRate | number:'1.0-2' }}%</div>
          </div>

          <div class="progress-track" aria-label="Execution progress">
            <div class="progress-segment progress-success" [style.width.%]="successPercent()"></div>
            <div class="progress-segment progress-failed" [style.width.%]="failedPercent()"></div>
            <div class="progress-segment progress-pending" [style.width.%]="pendingPercent()"></div>
          </div>

          <div class="metrics-grid">
            <div class="metric-card">
              <span class="metric-label">Total Tasks</span>
              <strong>{{ metrics()!.totalTasks }}</strong>
            </div>
            <div class="metric-card metric-success">
              <span class="metric-label">Success</span>
              <strong>{{ metrics()!.successCount }}</strong>
            </div>
            <div class="metric-card metric-failed">
              <span class="metric-label">Failed</span>
              <strong>{{ metrics()!.failedCount }}</strong>
            </div>
            <div class="metric-card metric-pending">
              <span class="metric-label">Pending</span>
              <strong>{{ metrics()!.pendingCount }}</strong>
            </div>
            <div class="metric-card">
              <span class="metric-label">Total Duration</span>
              <strong>{{ formatDurationSeconds(metrics()!.totalDurationSeconds) }}</strong>
            </div>
          </div>
        </section>
      }

      <section class="actions-bar">
        <button class="btn btn-danger-soft" [disabled]="!canCancel() || actionLoading()" (click)="showCancelConfirm.set(true)">Stop Run</button>
        <button class="btn" [disabled]="!canResume() || actionLoading()" (click)="showResumeConfirm.set(true)">Resume</button>
      </section>

      @if (showStoppingNotice()) {
        <div class="alert alert-warning">Stop requested. Running tasks may continue until they finish, but no new pending tasks will be scheduled.</div>
      }

      <section class="data-panel">
        <div class="panel-header">
          <div class="panel-title-wrap">
            <div class="panel-title">Task Executions</div>
            <div class="panel-subtitle">Latest execution outcome per task within this BoxRun.</div>
          </div>
        </div>
        <div class="panel-body" style="padding:0">
        <app-task-table [tasks]="tasks()" (viewError)="openError($event)" (viewLogs)="openLogs($event)" />
        </div>
      </section>
      }
    </div>

    <app-error-modal
      [visible]="errorTask() !== null"
      [error]="errorTask()?.error || ''"
      [stackTrace]="errorTask()?.stackTrace || ''"
      (close)="closeError()"
    />

    <app-task-logs-modal
      [visible]="logTask() !== null"
      [taskName]="logTask()?.name || ''"
      [logs]="taskLogs()"
      [loading]="logsLoading()"
      [error]="logsError()"
      (close)="closeLogs()"
    />

    <app-confirm-modal
      [visible]="showResumeConfirm()"
      title="Resume BoxRun"
      message="Failed tasks will be re-executed. Are you sure you want to resume this workflow?"
      confirmLabel="Resume"
      (confirmed)="confirmResume()"
      (cancelled)="showResumeConfirm.set(false)"
    />

    <app-confirm-modal
      [visible]="showCancelConfirm()"
      title="Stop Current Run"
      message="This will stop scheduling remaining tasks for this run. Tasks already running will be allowed to finish. Do you want to continue?"
      confirmLabel="Stop Run"
      (confirmed)="confirmCancel()"
      (cancelled)="showCancelConfirm.set(false)"
    />
  `,
  styles: [`
    .back-link { color: var(--text-2); text-decoration: none; font-size: .85rem; }
    .back-link:hover { text-decoration: underline; }
    .page-subtitle { margin-top:.35rem; }
    .metrics-panel { margin: 0 0 1rem; padding: 1rem; border:1px solid var(--border); background:var(--bg-surface); border-radius:var(--radius-2); }
    .metrics-header { display:flex; justify-content:space-between; align-items:center; gap:.75rem; margin-bottom:.75rem; }
    .metrics-rate { font-size:1.35rem; font-weight:800; color:var(--text-1); }
    .progress-track { display:flex; width:100%; height:12px; background:var(--bg-muted); border-radius:999px; overflow:hidden; margin-bottom:.9rem; }
    .progress-segment { height:100%; }
    .progress-success { background:#16a34a; }
    .progress-failed { background:#dc2626; }
    .progress-pending { background:#f59e0b; }
    .metrics-grid { display:grid; grid-template-columns:repeat(5,minmax(0,1fr)); gap:.75rem; }
    .metric-card { border:1px solid var(--border); background:var(--bg-muted); border-radius:var(--radius-1); padding:.75rem; }
    .metric-label { display:block; font-size:.72rem; text-transform:uppercase; color:var(--text-3); margin-bottom:.25rem; }
    .metric-success strong { color:#166534; }
    .metric-failed strong { color:#991b1b; }
    .metric-pending strong { color:#92400e; }
    .actions-bar { display:flex; align-items:center; margin: 1rem 0; gap:.75rem; }
    @media (max-width: 900px) {
      .metrics-grid { grid-template-columns:repeat(2,minmax(0,1fr)); }
      .actions-bar { flex-direction:column; align-items:flex-start; }
    }
    @media (max-width: 640px) {
      .metrics-grid { grid-template-columns:1fr; }
    }
  `]
})
export class BoxRunDetailComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private executionService = inject(ExecutionService);
  private pollSub?: Subscription;

  boxRunId = signal(0);
  run = signal<BoxRunDto | null>(null);
  tasks = signal<BoxRunTaskExecutionDto[]>([]);
  metrics = signal<BoxRunMetricsDto | null>(null);
  loading = signal(true);
  actionLoading = signal(false);
  error = signal('');

  errorTask = signal<BoxRunTaskExecutionDto | null>(null);
  logTask = signal<BoxRunTaskExecutionDto | null>(null);
  taskLogs = signal<TaskExecutionLogDto[]>([]);
  logsLoading = signal(false);
  logsError = signal('');
  showCancelConfirm = signal(false);
  showResumeConfirm = signal(false);

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('boxRunId'));
    this.boxRunId.set(id);
    this.reload();
    this.startPolling();
  }

  ngOnDestroy(): void {
    this.pollSub?.unsubscribe();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set('');
    this.loadBoxRunState(true);
  }

  startPolling(): void {
    this.pollSub = interval(4000)
      .pipe(switchMap(() => this.executionService.getBoxRun(this.boxRunId())))
      .subscribe({
        next: run => {
          this.run.set(run);
          this.loadBoxRunState(false, run);
        },
        error: () => void 0
      });
  }

  completedTasks(): number {
    return this.tasks().filter(t => t.status !== 'Pending' && t.status !== 'Running').length;
  }

  displayStatus(): BoxRunStatus {
    const run = this.run();
    if (!run) return 'Pending';
    return run.isCancellationRequested && run.status === 'Running'
      ? 'Stopping'
      : run.status;
  }

  canResume(): boolean {
    const run = this.run();
    const list = this.tasks();
    if (!run || run.isCancellationRequested || list.some(t => t.status === 'Running')) return false;
    return list.some(t => t.status === 'Failed') || run.status === 'Cancelled';
  }

  canCancel(): boolean {
    const run = this.run();
    if (!run) return false;
    return !run.isCancellationRequested && run.status === 'Running';
  }

  showStoppingNotice(): boolean {
    const run = this.run();
    return !!run && run.isCancellationRequested === true && run.status === 'Running';
  }

  confirmCancel(): void {
    this.showCancelConfirm.set(false);
    this.actionLoading.set(true);
    this.executionService.cancelBoxRun(this.boxRunId()).subscribe({
      next: () => {
        this.actionLoading.set(false);
        this.startPolling();
        this.reload();
      },
      error: () => {
        this.actionLoading.set(false);
      }
    });
  }

  confirmResume(): void {
    this.showResumeConfirm.set(false);
    this.actionLoading.set(true);
    this.executionService.resumeBoxRun(this.boxRunId()).subscribe({
      next: () => {
        this.actionLoading.set(false);
        this.startPolling();
        this.reload();
      },
      error: () => {
        this.actionLoading.set(false);
      }
    });
  }

  openError(task: BoxRunTaskExecutionDto): void {
    this.errorTask.set(task);
  }

  openLogs(task: BoxRunTaskExecutionDto): void {
    if (!task.executionId) return;

    this.logTask.set(task);
    this.taskLogs.set([]);
    this.logsError.set('');
    this.logsLoading.set(true);

    this.executionService.getTaskExecutionLogs(task.executionId).subscribe({
      next: logs => {
        this.taskLogs.set(logs);
        this.logsLoading.set(false);
      },
      error: () => {
        this.logsError.set('Failed to load execution logs.');
        this.logsLoading.set(false);
      }
    });
  }

  closeError(): void {
    this.errorTask.set(null);
  }

  closeLogs(): void {
    this.logTask.set(null);
    this.taskLogs.set([]);
    this.logsLoading.set(false);
    this.logsError.set('');
  }

  successPercent(): number {
    const metrics = this.metrics();
    if (!metrics || metrics.totalTasks === 0) return 0;
    return metrics.successCount / metrics.totalTasks * 100;
  }

  failedPercent(): number {
    const metrics = this.metrics();
    if (!metrics || metrics.totalTasks === 0) return 0;
    return metrics.failedCount / metrics.totalTasks * 100;
  }

  pendingPercent(): number {
    const metrics = this.metrics();
    if (!metrics || metrics.totalTasks === 0) return 0;
    return metrics.pendingCount / metrics.totalTasks * 100;
  }

  formatDurationSeconds(value?: number | null): string {
    if (value == null) return '--';
    const totalSeconds = Math.max(0, Math.floor(value));
    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;
    if (hours > 0) return `${hours}h ${minutes}m ${seconds}s`;
    if (minutes > 0) return `${minutes}m ${seconds}s`;
    return `${seconds}s`;
  }

  private loadBoxRunState(showError: boolean, knownRun?: BoxRunDto): void {
    const handleRun = (run: BoxRunDto) => {
      this.run.set(run);
      this.executionService.getBoxRunMetrics(this.boxRunId()).subscribe({
        next: metrics => {
          this.metrics.set(metrics);
          this.executionService.getBoxRunTasks(this.boxRunId()).subscribe({
            next: tasks => {
              this.tasks.set(tasks);
              this.loading.set(false);
              if (run.status === 'Completed' || run.status === 'Failed' || run.status === 'Partial' || run.status === 'Cancelled') {
                this.pollSub?.unsubscribe();
              }
            },
            error: () => {
              if (showError) {
                this.error.set('Failed to load task execution details.');
                this.loading.set(false);
              }
            }
          });
        },
        error: () => {
          if (showError) {
            this.error.set('Failed to load execution metrics.');
            this.loading.set(false);
          }
        }
      });
    };

    if (knownRun) {
      handleRun(knownRun);
      return;
    }

    this.executionService.getBoxRun(this.boxRunId()).subscribe({
      next: run => handleRun(run),
      error: () => {
        if (showError) {
          this.error.set('Failed to load BoxRun details.');
          this.loading.set(false);
        }
      }
    });
  }

  formatTime(value?: string): string {
    if (!value) return '--';
    return new Date(value).toLocaleString();
  }
}
