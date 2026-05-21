import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ButtonDirective } from 'ui-design-system';
import { Subscription, interval } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { BoxRunDto, BoxRunStatus, BoxRunMetricsDto, BoxRunTaskExecutionDto, TaskExecutionLogDto } from '../../models/models';
import { ExecutionService } from '../../services/execution.service';
import { StatusBadgeComponent } from '../../components/status-badge/status-badge.component';
import { TaskTableComponent } from '../../components/task-table/task-table.component';
import { ErrorModalComponent } from '../../components/error-modal/error-modal.component';
import { ConfirmModalComponent } from '../../components/confirm-modal/confirm-modal.component';
import { TaskLogsModalComponent } from '../../components/task-logs-modal/task-logs-modal.component';
import { TranslatePipe } from '../../shared/translate.pipe';

@Component({
  selector: 'app-box-run-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, StatusBadgeComponent, TaskTableComponent, ErrorModalComponent, ConfirmModalComponent, TaskLogsModalComponent, ButtonDirective, TranslatePipe],
  templateUrl: './box-run-detail.component.html',
  styles: [`
    .metrics-rate { font-size:1.35rem; font-weight:800; color:var(--text-1); }
    .progress-track { display:flex; width:100%; height:12px; background:var(--bg-muted); border-radius:999px; overflow:hidden; margin-bottom:.9rem; }
    .progress-segment { height:100%; }
    .progress-success { background:var(--ui-success-text); }
    .progress-failed { background:var(--ui-danger-text); }
    .progress-pending { background:var(--ui-warning-text); }
    .ui-feedback--warning { background:var(--ui-warning-bg); border-color:var(--ui-warning-border); color:var(--ui-warning-text); }
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
