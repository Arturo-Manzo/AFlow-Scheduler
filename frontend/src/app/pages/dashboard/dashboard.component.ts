import { Component, HostListener, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LogsService } from '../../services/logs.service';
import { BoxesService } from '../../services/boxes.service';
import { ExecutionDto, BoxDto } from '../../models/models';
import { detectUserTimeZone, formatUtcWithZoneContext, getDateKeyInTimeZone } from '../../shared/timezone-utils';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="page-header">
      <div>
        <h1>Dashboard</h1>
        <div class="page-subtitle">Times shown in {{ userTimeZone }}</div>
      </div>
    </div>

    <div class="stats-row">
      <div class="stat-card">
        <span class="stat-value">{{ loadingBoxes() ? '--' : boxes().length }}</span>
        <span class="stat-label">Total Boxes</span>
      </div>
      <div class="stat-card stat-warning">
        <span class="stat-value">{{ loadingLogs() ? '--' : runningCount() }}</span>
        <span class="stat-label">Running Now</span>
      </div>
      <div class="stat-card stat-success">
        <span class="stat-value">{{ loadingLogs() ? '--' : successToday() }}</span>
        <span class="stat-label">Succeeded Today</span>
      </div>
      <div class="stat-card stat-danger">
        <span class="stat-value">{{ loadingLogs() ? '--' : failedToday() }}</span>
        <span class="stat-label">Failed Today</span>
      </div>
    </div>

    <p class="section-title">Recent Executions</p>

    @if (loadingLogs()) {
      <div class="loading-state"><span class="spinner"></span> Loading logs...</div>
    } @else if (logsError()) {
      <div class="alert alert-danger">{{ logsError() }}</div>
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
              <td><span [class]="'badge badge-' + log.status.toLowerCase()">{{ log.status }}</span></td>
              <td>{{ formatExecutionTime(log.startedAt, log.boxTimeZoneId, 'short') }}</td>
              <td>{{ log.durationSeconds != null ? log.durationSeconds + 's' : '--' }}</td>
            </tr>
          }
        </tbody>
      </table>
    }

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
                <span><span [class]="'badge badge-' + selected()!.status.toLowerCase()">{{ selected()!.status }}</span></span>
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
  styles: [`
    .page-subtitle { margin-top:.2rem; font-size:.84rem; color:var(--text-3); }
    .box-cell { font-size:.85rem; color:var(--text-2); }
    .row-selected { background: color-mix(in srgb, var(--primary, #4f6ef7) 8%, white); }
    .exec-meta-grid { display:grid; grid-template-columns:1fr 1fr; gap:.5rem .75rem; }
    .exec-kv { display:flex; flex-direction:column; gap:.15rem; }
    .exec-kv-full { grid-column:1 / -1; }
    .exec-key { font-size:.7rem; font-weight:700; text-transform:uppercase; letter-spacing:.05em; color:var(--text-3); }
  `]
})
export class DashboardComponent implements OnInit {
  private logsService = inject(LogsService);
  private boxesService = inject(BoxesService);

  readonly userTimeZone = detectUserTimeZone();

  boxes = signal<BoxDto[]>([]);
  logs = signal<ExecutionDto[]>([]);
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
    this.boxesService.getAll().subscribe({
      next: (bs: BoxDto[]) => {
        this.boxes.set(bs);
        this.loadingBoxes.set(false);
      },
      error: () => this.loadingBoxes.set(false)
    });

    this.logsService.getLatest(50).subscribe({
      next: data => {
        this.logs.set(data.slice(0, 10));
        const today = getDateKeyInTimeZone(new Date(), this.userTimeZone);
        const todayLogs = data.filter(log => getDateKeyInTimeZone(log.startedAt, this.userTimeZone) === today);
        this.runningCount.set(data.filter(log => log.status === 'Running').length);
        this.successToday.set(todayLogs.filter(log => log.status === 'Success').length);
        this.failedToday.set(todayLogs.filter(log => log.status === 'Failed' || log.status === 'Timeout').length);
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
}
