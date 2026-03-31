import { Component, HostListener, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LogsService } from '../../services/logs.service';
import { ExecutionDto } from '../../models/models';
import { detectUserTimeZone, formatUtcWithZoneContext } from '../../shared/timezone-utils';

@Component({
  selector: 'app-logs',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="page-header">
      <div>
        <h1>Execution Logs</h1>
        <div class="page-subtitle">Times shown in {{ userTimeZone }}</div>
      </div>
      <div class="page-actions">
        <label style="font-size:.85rem;color:var(--text-2);display:flex;align-items:center;gap:.4rem">
          Show
          <select (change)="changeLimit($event)" style="padding:.3rem .5rem;border:1px solid var(--border);border-radius:var(--radius-1);font-size:.85rem;background:var(--bg-surface)">
            <option value="20">20</option>
            <option value="50">50</option>
            <option value="100">100</option>
          </select>
          records
        </label>
        <button class="btn" (click)="reload()">Refresh</button>
      </div>
    </div>

    @if (loading()) {
      <div class="loading-state"><span class="spinner"></span> Loading logs...</div>
    } @else if (loadError()) {
      <div class="alert alert-danger">
        {{ loadError() }}
        <button class="btn btn-ghost btn-sm" style="margin-left:auto" (click)="reload()">Retry</button>
      </div>
    } @else if (logs().length === 0) {
      <p class="empty-state">No execution records found.</p>
    } @else {
      <table class="data-table">
        <thead>
          <tr>
            <th>#</th>
            <th>Task</th>
            <th>Box</th>
            <th>Source</th>
            <th>Status</th>
            <th>Started</th>
            <th>Duration</th>
            <th>Exit</th>
          </tr>
        </thead>
        <tbody>
          @for (log of logs(); track log.executionId) {
            <tr
              (click)="select(log)"
              [class.row-selected]="selected()?.executionId === log.executionId"
              style="cursor:pointer"
            >
              <td class="id-cell">{{ log.executionId }}</td>
              <td><strong>{{ log.taskName }}</strong></td>
              <td class="box-cell">{{ log.boxName || '--' }}</td>
              <td><span class="badge badge-neutral">{{ log.triggerSource }}</span></td>
              <td><span [class]="'badge badge-' + log.status.toLowerCase()">{{ log.status }}</span></td>
              <td>{{ formatExecutionTime(log.startedAt, log.boxTimeZoneId, 'short') }}</td>
              <td>{{ log.durationSeconds != null ? log.durationSeconds + 's' : '--' }}</td>
              <td>{{ log.exitCode ?? '--' }}</td>
            </tr>
          }
        </tbody>
      </table>
    }

    @if (selected()) {
      <div class="detail-panel">
        <div class="detail-header">
          <div>
            <strong>{{ selected()!.taskName }}</strong>
            <span [class]="'badge badge-' + selected()!.status.toLowerCase()" style="margin-left:.5rem">{{ selected()!.status }}</span>
            <div class="detail-meta">
              {{ selected()!.boxName || '--' }}
              &nbsp;·&nbsp;
              <span class="badge badge-neutral" style="font-size:.7rem">{{ selected()!.triggerSource }}</span>
            </div>
          </div>
          <button class="btn btn-ghost btn-sm" (click)="selected.set(null)">Close</button>
        </div>
        <div class="detail-meta-grid">
          <div class="detail-kv"><span class="detail-key">Started</span><span>{{ formatExecutionTime(selected()!.startedAt, selected()!.boxTimeZoneId, 'medium') }}</span></div>
          <div class="detail-kv"><span class="detail-key">Ended</span><span>{{ selected()!.endedAt ? formatExecutionTime(selected()!.endedAt, selected()!.boxTimeZoneId, 'medium') : '--' }}</span></div>
          <div class="detail-kv"><span class="detail-key">Duration</span><span>{{ selected()!.durationSeconds != null ? selected()!.durationSeconds + 's' : '--' }}</span></div>
          <div class="detail-kv"><span class="detail-key">Exit Code</span><span>{{ selected()!.exitCode ?? '--' }}</span></div>
          @if (selected()!.isStale) {
            <div class="detail-kv"><span class="detail-key">Stale</span><span>Yes</span></div>
          }
          @if (displayRequestedBy(selected()!)) {
            <div class="detail-kv"><span class="detail-key">Requested By</span><span>{{ displayRequestedBy(selected()!) }}</span></div>
          }
          @if (selected()!.reason) {
            <div class="detail-kv detail-kv-full"><span class="detail-key">Reason</span><span>{{ selected()!.reason }}</span></div>
          }
          @if (selected()!.errorMessage) {
            <div class="detail-kv detail-kv-full"><span class="detail-key">Error</span><span>{{ selected()!.errorMessage }}</span></div>
          }
        </div>
        <div class="detail-section">
          <p class="section-title" style="margin-top:0">Standard Output</p>
          <pre>{{ selected()!.stdOut || '(empty)' }}</pre>
        </div>
        <div class="detail-section">
          <p class="section-title">Standard Error</p>
          <pre class="pre-err">{{ selected()!.stdErr || '(empty)' }}</pre>
        </div>
      </div>
    }
  `,
  styles: [`
    .page-subtitle { margin-top:.2rem; font-size:.84rem; color:var(--text-3); }
    .id-cell { color: var(--text-3); font-size: .8rem; }
    .box-cell { font-size:.85rem; color:var(--text-2); }
    .row-selected { background: color-mix(in srgb, var(--primary, #4f6ef7) 8%, white); }
    .detail-panel {
      margin-top: 1.25rem;
      background: var(--bg-surface);
      border-radius: var(--radius-2);
      box-shadow: var(--shadow-1);
      border: 1px solid var(--border);
      overflow: hidden;
    }
    .detail-header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      padding: .75rem 1rem;
      border-bottom: 1px solid var(--border);
      background: var(--bg-muted);
      font-size: .875rem;
      gap: 1rem;
    }
    .detail-meta {
      color: var(--text-3);
      margin-top: .25rem;
      font-size: .8rem;
    }
    .detail-section { padding: 1rem; }
    pre {
      background: var(--bg-muted);
      border: 1px solid var(--border);
      border-radius: var(--radius-1);
      padding: .75rem 1rem;
      font-family: var(--font-mono);
      font-size: .8rem;
      overflow-x: auto;
      max-height: 180px;
      white-space: pre-wrap;
      word-break: break-all;
      color: var(--text-1);
    }
    pre.pre-err { color: var(--danger); }
    .detail-meta-grid { display:grid; grid-template-columns:1fr 1fr; gap:.4rem .75rem; padding:.75rem 1rem; border-bottom:1px solid var(--border); }
    .detail-kv { display:flex; flex-direction:column; gap:.1rem; }
    .detail-kv-full { grid-column:1 / -1; }
    .detail-key { font-size:.68rem; font-weight:700; text-transform:uppercase; letter-spacing:.05em; color:var(--text-3); }
  `]
})
export class LogsComponent implements OnInit {
  private logsService = inject(LogsService);

  readonly userTimeZone = detectUserTimeZone();

  logs = signal<ExecutionDto[]>([]);
  loading = signal(true);
  loadError = signal('');
  selected = signal<ExecutionDto | null>(null);
  limit = signal(20);

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.selected.set(null);
  }

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.loadError.set('');
    this.logsService.getLatest(this.limit()).subscribe({
      next: data => {
        this.logs.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.loadError.set('Failed to load logs. Check API connection.');
        this.loading.set(false);
      }
    });
  }

  changeLimit(event: Event): void {
    this.limit.set(Number((event.target as HTMLSelectElement).value));
    this.reload();
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
