import { Component, HostListener, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LogsService } from '../../services/logs.service';
import { ExecutionDto } from '../../models/models';

@Component({
  selector: 'app-logs',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="page-header">
      <h1>Execution Logs</h1>
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
            <th>Source</th>
            <th>Status</th>
            <th>Started</th>
            <th>Ended</th>
            <th>Duration</th>
            <th>Exit Code</th>
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
              <td><span class="badge badge-neutral">{{ log.triggerSource }}</span></td>
              <td><span [class]="'badge badge-' + log.status.toLowerCase()">{{ log.status }}</span></td>
              <td>{{ log.startedAt | date:'medium' }}</td>
              <td>{{ log.endedAt ? (log.endedAt | date:'medium') : '-' }}</td>
              <td>{{ log.durationSeconds != null ? log.durationSeconds + 's' : '-' }}</td>
              <td>{{ log.exitCode }}</td>
            </tr>
          }
        </tbody>
      </table>
    }

    @if (selected()) {
      <div class="detail-panel">
        <div class="detail-header">
          <div>
            <strong>Execution #{{ selected()!.executionId }} - {{ selected()!.taskName }}</strong>
            <div class="detail-meta">Trigger: {{ selected()!.triggerSource }} | Status: {{ selected()!.status }}</div>
          </div>
          <button class="btn btn-ghost btn-sm" (click)="selected.set(null)">Close</button>
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
    .id-cell { color: var(--text-3); font-size: .8rem; }
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
  `]
})
export class LogsComponent implements OnInit {
  private logsService = inject(LogsService);

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
      next: (data) => {
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
}
