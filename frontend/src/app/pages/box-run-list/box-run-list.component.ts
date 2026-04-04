import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Subscription, interval } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { BoxRunDto, BoxRunStatus } from '../../models/models';
import { ExecutionService } from '../../services/execution.service';
import { StatusBadgeComponent } from '../../components/status-badge/status-badge.component';
import { ConfirmModalComponent } from '../../components/confirm-modal/confirm-modal.component';

@Component({
  selector: 'app-box-run-list',
  standalone: true,
  imports: [CommonModule, RouterLink, StatusBadgeComponent, ConfirmModalComponent],
  template: `
    <div class="view-shell">
      <div class="view-hero">
        <div class="view-hero-main">
          <div class="view-eyebrow">Operational Execution Monitor</div>
          <h1>Workflow Executions</h1>
          <p class="view-description">
            Latest active or non-terminal BoxRuns, optimized for operational control, resume decisions and stop-request tracking.
          </p>
        </div>
        <div class="view-hero-kpi">
          <span class="kpi-value">{{ loading() ? '--' : runs().length }}</span>
          <span class="kpi-label">Visible Runs</span>
        </div>
        <div class="view-hero-kpi">
          <span class="kpi-value">{{ loading() ? '--' : runningCount() }}</span>
          <span class="kpi-label">Running</span>
        </div>
        <div class="view-hero-kpi">
          <span class="kpi-value">{{ loading() ? '--' : resumableCount() }}</span>
          <span class="kpi-label">Resumable</span>
        </div>
      </div>

      <section class="data-panel">
        <div class="panel-header">
          <div class="panel-title-wrap">
            <div class="panel-title">Active BoxRun Queue</div>
            <div class="panel-subtitle">One latest run per Box, excluding fully completed workflows.</div>
          </div>
          <div class="panel-toolbar">
            <button class="btn" (click)="reload()">Refresh</button>
          </div>
        </div>

        @if (loading()) {
          <div class="loading-state"><span class="spinner"></span> Loading executions...</div>
        } @else if (error()) {
          <div class="panel-body"><div class="alert alert-danger">{{ error() }}</div></div>
        } @else if (runs().length === 0) {
          <p class="empty-state">No active executions. All workflows completed successfully.</p>
        } @else {
          <table class="data-table">
            <thead>
              <tr>
                <th>Box Name</th>
                <th>Status</th>
                <th>Trigger</th>
                <th>Start Time</th>
                <th>End Time</th>
                <th>Duration</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (run of runs(); track run.id) {
                <tr>
                  <td>
                    <strong>{{ run.boxName }}</strong>
                    <div class="row-meta">BoxRun #{{ run.id }}</div>
                  </td>
                  <td><app-status-badge [status]="displayStatus(run)" /></td>
                  <td><span class="badge badge-neutral">{{ run.triggerSource }}</span></td>
                  <td>{{ formatTime(run.startTime) }}</td>
                  <td>{{ formatTime(run.endTime) }}</td>
                  <td>{{ run.durationSeconds != null ? run.durationSeconds + 's' : '--' }}</td>
                    <td>
                      <div class="table-actions">
                        <a class="btn btn-sm btn-view" [routerLink]="['/executions', run.id]">View Details</a>
                        <button class="btn btn-sm btn-danger-soft" [disabled]="!canCancelFromList(run) || actionLoading()" (click)="pendingCancelId.set(run.id)">Stop Run</button>
                        <button class="btn btn-sm" [disabled]="!canResumeFromList(run) || actionLoading()" (click)="pendingResumeId.set(run.id)">Resume</button>
                      </div>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
      </section>
    </div>

    <app-confirm-modal
      [visible]="pendingResumeId() !== null"
      title="Resume BoxRun"
      message="Failed tasks will be re-executed. Are you sure you want to resume this workflow?"
      confirmLabel="Resume"
      (confirmed)="confirmResume()"
      (cancelled)="pendingResumeId.set(null)"
    />

    <app-confirm-modal
      [visible]="pendingCancelId() !== null"
      title="Stop Current Run"
      message="This will stop scheduling remaining tasks for this run. Tasks already running will be allowed to finish. Do you want to continue?"
      confirmLabel="Stop Run"
      (confirmed)="confirmCancel()"
      (cancelled)="pendingCancelId.set(null)"
    />
  `,
  styles: [`
    .row-meta { margin-top:.2rem; color:var(--text-3); font-size:.75rem; }
  `]
})
export class BoxRunListComponent implements OnInit, OnDestroy {
  private executionService = inject(ExecutionService);
  private pollSub?: Subscription;

  runs = signal<BoxRunDto[]>([]);
  loading = signal(true);
  error = signal('');
  actionLoading = signal(false);
  pendingResumeId = signal<number | null>(null);
  pendingCancelId = signal<number | null>(null);

  ngOnInit(): void {
    this.reload();
    this.pollSub = interval(5000)
      .pipe(switchMap(() => this.executionService.getBoxRuns(150)))
      .subscribe({
        next: runs => this.runs.set(this.activeOnly(runs)),
        error: () => void 0
      });
  }

  ngOnDestroy(): void {
    this.pollSub?.unsubscribe();
  }

  private activeOnly(runs: BoxRunDto[]): BoxRunDto[] {
    // Keep only latest run per box; if latest is Completed, hide the box entirely
    const latestPerBox = new Map<number, BoxRunDto>();
    for (const run of runs) {
      const existing = latestPerBox.get(run.boxId);
      if (!existing || run.id > existing.id) {
        latestPerBox.set(run.boxId, run);
      }
    }
    return Array.from(latestPerBox.values())
      .filter(r => r.status !== 'Completed')
      .sort((a, b) => b.id - a.id);
  }

  reload(): void {
    this.loading.set(true);
    this.error.set('');
    this.executionService.getBoxRuns(150).subscribe({
      next: runs => {
        this.runs.set(this.activeOnly(runs));
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load BoxRun executions.');
        this.loading.set(false);
      }
    });
  }

  canResumeFromList(run: BoxRunDto): boolean {
    return !run.isCancellationRequested && (run.status === 'Failed' || run.status === 'Partial' || run.status === 'Cancelled');
  }

  canCancelFromList(run: BoxRunDto): boolean {
    return !run.isCancellationRequested && run.status === 'Running';
  }

  displayStatus(run: BoxRunDto): BoxRunStatus {
    return run.isCancellationRequested && run.status === 'Running' ? 'Stopping' : run.status;
  }

  confirmCancel(): void {
    const id = this.pendingCancelId();
    this.pendingCancelId.set(null);
    if (id === null) return;
    this.actionLoading.set(true);
    this.executionService.cancelBoxRun(id).subscribe({
      next: () => {
        this.actionLoading.set(false);
        this.reload();
      },
      error: () => {
        this.actionLoading.set(false);
      }
    });
  }

  confirmResume(): void {
    const id = this.pendingResumeId();
    this.pendingResumeId.set(null);
    if (id === null) return;
    this.actionLoading.set(true);
    this.executionService.resumeBoxRun(id).subscribe({
      next: () => {
        this.actionLoading.set(false);
        this.reload();
      },
      error: () => {
        this.actionLoading.set(false);
      }
    });
  }

  formatTime(value?: string): string {
    if (!value) return '--';
    return new Date(value).toLocaleString();
  }

  runningCount(): number {
    return this.runs().filter(run => run.status === 'Running').length;
  }

  resumableCount(): number {
    return this.runs().filter(run => run.status === 'Failed' || run.status === 'Partial' || run.status === 'Cancelled').length;
  }
}
