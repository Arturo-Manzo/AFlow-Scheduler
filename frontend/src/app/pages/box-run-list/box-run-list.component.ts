import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ButtonDirective } from 'ui-design-system';
import { Subscription, interval } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { BoxRunDto, BoxRunStatus } from '../../models/models';
import { ExecutionService } from '../../services/execution.service';
import { StatusBadgeComponent } from '../../components/status-badge/status-badge.component';
import { ConfirmModalComponent } from '../../components/confirm-modal/confirm-modal.component';

@Component({
  selector: 'app-box-run-list',
  standalone: true,
  imports: [CommonModule, RouterLink, StatusBadgeComponent, ConfirmModalComponent, ButtonDirective],
  templateUrl: './box-run-list.component.html',
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
