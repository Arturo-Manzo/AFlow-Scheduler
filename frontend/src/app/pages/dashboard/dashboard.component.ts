import { Component, DestroyRef, HostListener, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { forkJoin } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ExecutionService } from '../../services/execution.service';
import { BoxesService } from '../../services/boxes.service';
import { ExecutionDto, BoxDto, RunningExecutionDto } from '../../models/models';
import { StatusBadgeComponent } from '../../components/status-badge/status-badge.component';
import { detectUserTimeZone, formatUtcWithZoneContext, getDateKeyInTimeZone } from '../../shared/timezone-utils';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, StatusBadgeComponent],
  template: `
    <div class="view-shell">
      <div class="view-hero">
        <div class="view-hero-main">
          <div class="view-eyebrow">System Summary Overview</div>
          <h1>Execution Metrics</h1>
          <p class="view-description">
            Aggregate workflow activity, current load, and recent task movement across the scheduler control plane.
            All timestamps are normalized to {{ userTimeZone }} for this session.
          </p>
        </div>
        <div class="view-hero-kpi">
          <span class="kpi-value">{{ loadingBoxes() ? '--' : boxes().length }}</span>
          <span class="kpi-label">Registered Boxes</span>
        </div>
        <div class="view-hero-kpi">
          <span class="kpi-value">{{ loadingLogs() ? '--' : runningCount() }}</span>
          <span class="kpi-label">Running Now</span>
        </div>
        <div class="view-hero-kpi">
          <span class="kpi-value">{{ loadingLogs() ? '--' : successToday() }}</span>
          <span class="kpi-label">Succeeded Today</span>
        </div>
      </div>

      <div class="stats-row">
        <div class="stat-card">
          <span class="stat-value">{{ loadingLogs() ? '--' : logs().length }}</span>
          <span class="stat-label">Recent Records Loaded</span>
        </div>
        <div class="stat-card stat-warning">
          <span class="stat-value">{{ loadingLogs() ? '--' : running().length }}</span>
          <span class="stat-label">Live Executions</span>
        </div>
        <div class="stat-card stat-success">
          <span class="stat-value">{{ loadingBoxes() ? '--' : activeBoxes() }}</span>
          <span class="stat-label">Active Boxes</span>
        </div>
        <div class="stat-card stat-danger">
          <span class="stat-value">{{ loadingLogs() ? '--' : failedToday() }}</span>
          <span class="stat-label">Failed Today</span>
        </div>
      </div>

      <section class="data-panel">
        <div class="panel-header">
          <div class="panel-title-wrap">
            <div class="panel-title">Recent Task Executions</div>
            <div class="panel-subtitle">Latest execution records available for inspection from the dashboard.</div>
          </div>
          <div class="microcopy">Showing {{ logs().length }} rows</div>
        </div>

        @if (loadingLogs()) {
          <div class="loading-state"><span class="spinner"></span> Loading logs...</div>
        } @else if (logsError()) {
          <div class="panel-body"><div class="alert alert-danger">{{ logsError() }}</div></div>
        } @else if (logs().length === 0) {
          <p class="empty-state">No executions recorded yet.</p>
        } @else {
          <table class="data-table">
            <thead>
              <tr>
                <th>Task</th>
                <th>Box</th>
                <th>Trigger</th>
                <th>Status</th>
                <th>Started</th>
                <th>Duration</th>
              </tr>
            </thead>
            <tbody>
              @for (log of logs(); track log.executionId) {
                <tr
                  (click)="select(log)"
                  [class.row-selected]="selected()?.executionId === log.executionId"
                  style="cursor:pointer"
                >
                  <td><strong>{{ log.taskName }}</strong></td>
                  <td class="box-cell">{{ log.boxName || '--' }}</td>
                  <td><span class="badge badge-neutral">{{ log.triggerSource }}</span></td>
                  <td><app-status-badge [status]="log.status" /></td>
                  <td>{{ formatExecutionTime(log.startedAt, log.boxTimeZoneId, 'short') }}</td>
                  <td>{{ log.durationSeconds != null ? log.durationSeconds + 's' : '--' }}</td>
                </tr>
              }
            </tbody>
          </table>
        }
      </section>
    </div>

    @if (selected()) {
      <div class="modal-overlay" role="dialog" aria-modal="true" (click)="selected.set(null)">
        <div class="modal" (click)="$event.stopPropagation()" style="max-width:560px;width:95vw">
          <div class="modal-header">
            <div>
              <h3 style="margin:0;font-size:1rem">{{ selected()!.taskName }}</h3>
              <div style="font-size:.8rem;color:var(--text-3);margin-top:.2rem">{{ selected()!.boxName || '--' }}</div>
            </div>
            <button type="button" class="modal-close" (click)="selected.set(null)" aria-label="Close">x</button>
          </div>
          <div class="modal-body">
            <div class="exec-meta-grid">
              <div class="exec-kv">
                <span class="exec-key">Status</span>
                <span><app-status-badge [status]="selected()!.status" /></span>
              </div>
              <div class="exec-kv">
                <span class="exec-key">Trigger</span>
                <span><span class="badge badge-neutral">{{ selected()!.triggerSource }}</span></span>
              </div>
              <div class="exec-kv">
                <span class="exec-key">Started</span>
                <span>{{ formatExecutionTime(selected()!.startedAt, selected()!.boxTimeZoneId, 'medium') }}</span>
              </div>
              <div class="exec-kv">
                <span class="exec-key">Ended</span>
                <span>{{ selected()!.endedAt ? formatExecutionTime(selected()!.endedAt, selected()!.boxTimeZoneId, 'medium') : '--' }}</span>
              </div>
              <div class="exec-kv">
                <span class="exec-key">Duration</span>
                <span>{{ selected()!.durationSeconds != null ? selected()!.durationSeconds + 's' : '--' }}</span>
              </div>
              @if (displayRequestedBy(selected()!)) {
                <div class="exec-kv">
                  <span class="exec-key">Requested By</span>
                  <span>{{ displayRequestedBy(selected()!) }}</span>
                </div>
              }
              @if (selected()!.reason) {
                <div class="exec-kv exec-kv-full">
                  <span class="exec-key">Reason</span>
                  <span>{{ selected()!.reason }}</span>
                </div>
              }
            </div>
          </div>
          <div class="modal-footer">
            <button class="btn" (click)="selected.set(null)">Close</button>
          </div>
        </div>
      </div>
    }
  `,
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  private executionService = inject(ExecutionService);
  private boxesService = inject(BoxesService);
  private destroyRef = inject(DestroyRef);

  readonly userTimeZone = detectUserTimeZone();

  boxes = signal<BoxDto[]>([]);
  logs = signal<ExecutionDto[]>([]);
  running = signal<RunningExecutionDto[]>([]);
  selected = signal<ExecutionDto | null>(null);
  loadingBoxes = signal(true);
  loadingLogs = signal(true);
  logsError = signal('');

  runningCount = signal(0);
  successToday = signal(0);
  failedToday = signal(0);

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.selected.set(null);
  }

  ngOnInit(): void {
    this.boxesService.getAll().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (bs: BoxDto[]) => {
        this.boxes.set(bs);
        this.loadingBoxes.set(false);
      },
      error: () => this.loadingBoxes.set(false)
    });

    forkJoin({
      latest: this.executionService.getLatest(50),
      running: this.executionService.getRunning()
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: ({ latest, running }) => {
        this.logs.set(latest.slice(0, 10));
        this.running.set(running);
        const today = getDateKeyInTimeZone(new Date(), this.userTimeZone);
        const todayLogs = latest.filter(log => getDateKeyInTimeZone(log.startedAt, this.userTimeZone) === today);
        this.runningCount.set(running.length);
        this.successToday.set(todayLogs.filter(log => log.status === 'Success').length);
        this.failedToday.set(todayLogs.filter(log => log.status === 'Failed').length);
        this.loadingLogs.set(false);
      },
      error: () => {
        this.logsError.set('Unable to load execution history. Check API connection.');
        this.loadingLogs.set(false);
      }
    });
  }

  select(log: ExecutionDto): void {
    this.selected.set(this.selected()?.executionId === log.executionId ? null : log);
  }

  formatExecutionTime(value: string | undefined, boxTimeZoneId: string | undefined, variant: 'short' | 'medium'): string {
    return formatUtcWithZoneContext(
      value,
      this.userTimeZone,
      boxTimeZoneId,
      variant === 'short'
        ? { dateStyle: 'short', timeStyle: 'short' }
        : { dateStyle: 'medium', timeStyle: 'short' },
      { timeStyle: 'short' }
    );
  }

  displayRequestedBy(log: ExecutionDto): string {
    return log.requestedByUsername || (log.requestedByUserId ? 'Unknown user' : '');
  }

  activeBoxes(): number {
    return this.boxes().filter(box => box.enabled).length;
  }
}
