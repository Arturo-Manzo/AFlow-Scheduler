import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Subscription, interval } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { BoxRun, BoxRunStatus } from '../../models/models';
import { ExecutionService } from '../../services/execution.service';
import { StatusBadgeComponent } from '../../components/status-badge/status-badge.component';
import { ConfirmModalComponent } from '../../components/confirm-modal/confirm-modal.component';

@Component({
  selector: 'app-box-run-list',
  standalone: true,
  imports: [CommonModule, RouterLink, StatusBadgeComponent, ConfirmModalComponent],
  template: `
    <div class="page-header">
      <div>
        <h1>Workflow Executions</h1>
        <div class="page-subtitle">Operational view of BoxRuns and status.</div>
      </div>
      <div class="page-actions">
        <button class="btn" (click)="reload()">Refresh</button>
      </div>
    </div>

    @if (loading()) {
      <div class="loading-state"><span class="spinner"></span> Loading executions...</div>
    } @else if (error()) {
      <div class="alert alert-danger">{{ error() }}</div>
    } @else if (runs().length === 0) {
      <p class="empty-state">No active executions. All workflows completed successfully.</p>
    } @else {
      <table class="data-table">
        <thead>
          <tr>
            <th>Box Name</th>
            <th>Status</th>
            <th>Start Time</th>
            <th>End Time</th>
            <th>Duration</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          @for (run of runs(); track run.id) {
            <tr>
              <td><strong>{{ run.boxName }}</strong></td>
              <td><app-status-badge [status]="displayStatus(run)" /></td>
              <td>{{ formatTime(run.startTime) }}</td>
              <td>{{ formatTime(run.endTime) }}</td>
              <td>{{ run.durationSeconds != null ? run.durationSeconds + 's' : '--' }}</td>
              <td class="table-actions">
                <a class="btn btn-sm btn-view" [routerLink]="['/executions', run.id]">View Details</a>
                <button class="btn btn-sm btn-danger-soft" [disabled]="!canCancelFromList(run) || actionLoading()" (click)="pendingCancelId.set(run.id)">Stop Run</button>
                <button class="btn btn-sm" [disabled]="!canResumeFromList(run) || actionLoading()" (click)="pendingResumeId.set(run.id)">Resume</button>
              </td>
            </tr>
          }
        </tbody>
      </table>
    }

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
    .page-subtitle { margin-top:.2rem; font-size:.84rem; color:var(--text-3); }
    .btn-view { background:var(--bg-muted);color:var(--text-2);border-color:var(--border); text-decoration:none; }
    .btn-view:hover { background:var(--border); }
    .btn-danger-soft { background:#fff1f2; color:#be123c; border-color:#fecdd3; }
    .btn-danger-soft:hover { background:#ffe4e6; }
  `]
})
export class BoxRunListComponent implements OnInit, OnDestroy {
  private executionService = inject(ExecutionService);
  private pollSub?: Subscription;

  runs = signal<BoxRun[]>([]);
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

  private activeOnly(runs: BoxRun[]): BoxRun[] {
    // Keep only latest run per box; if latest is Completed, hide the box entirely
    const latestPerBox = new Map<number, BoxRun>();
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

  canResumeFromList(run: BoxRun): boolean {
    return !run.isCancellationRequested && (run.status === 'Failed' || run.status === 'Partial' || run.status === 'Cancelled');
  }

  canCancelFromList(run: BoxRun): boolean {
    return !run.isCancellationRequested && run.status === 'Running';
  }

  displayStatus(run: BoxRun): BoxRunStatus {
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
}
