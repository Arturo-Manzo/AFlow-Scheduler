import { AfterViewChecked, Component, ElementRef, EventEmitter, Input, Output, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TaskExecutionLogDto } from '../../models/models';

@Component({
  selector: 'app-task-logs-modal',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (visible) {
      <div class="modal-overlay" role="dialog" aria-modal="true" (click)="close.emit()">
        <div class="modal task-logs-modal" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <div>
              <h3 style="margin:0;font-size:1rem">Task Logs</h3>
              <div class="modal-subtitle">{{ taskName || 'Task execution logs' }}</div>
            </div>
            <button type="button" class="modal-close" (click)="close.emit()" aria-label="Close">x</button>
          </div>
          <div class="modal-body">
            <div class="toolbar">
              <button class="btn btn-sm" type="button" (click)="toggleNewestFirst()">
                {{ newestFirst ? 'Show oldest first' : 'Show newest first' }}
              </button>
            </div>

            <div #logContainer class="logs-container">
              @if (loading) {
                <div class="empty-state">Loading logs...</div>
              } @else if (error) {
                <div class="alert alert-danger">{{ error }}</div>
              } @else if (orderedLogs().length === 0) {
                <div class="empty-state">No logs found for this execution.</div>
              } @else {
                <table class="data-table logs-table">
                  <thead>
                    <tr>
                      <th>Timestamp</th>
                      <th>Level</th>
                      <th>Message</th>
                      <th>Details</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (log of orderedLogs(); track log.id) {
                      <tr>
                        <td class="timestamp-cell">{{ formatTimestamp(log.timestamp) }}</td>
                        <td><span [class]="'badge log-level level-' + log.level.toLowerCase()">{{ log.level }}</span></td>
                        <td>{{ log.message }}</td>
                        <td>
                          @if (log.details) {
                            <button class="btn btn-sm" type="button" (click)="toggleDetails(log.id)">
                              {{ isExpanded(log.id) ? 'Hide Details' : 'Show Details' }}
                            </button>
                            @if (isExpanded(log.id)) {
                              <pre class="details-block">{{ log.details }}</pre>
                            }
                          } @else {
                            <span class="muted">--</span>
                          }
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              }
            </div>
          </div>
          <div class="modal-footer">
            <button class="btn" (click)="close.emit()">Close</button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .task-logs-modal { max-width: 980px; width: 96vw; }
    .modal-subtitle { margin-top:.2rem; color:var(--text-3); font-size:.82rem; }
    .toolbar { display:flex; justify-content:flex-end; margin-bottom:.75rem; }
    .logs-container { max-height: 60vh; overflow:auto; border:1px solid var(--border); border-radius:var(--radius-2); }
    .logs-table { margin:0; border:none; }
    .timestamp-cell { white-space:nowrap; color:var(--text-2); }
    .log-level { font-weight:700; font-size:.7rem; text-transform:uppercase; }
    .level-info { background:#f3f4f6; color:#4b5563; }
    .level-warning { background:#fef3c7; color:#92400e; }
    .level-error { background:#fee2e2; color:#991b1b; }
    .details-block {
      margin-top:.5rem;
      max-height:220px;
      overflow:auto;
      padding:.75rem;
      border:1px solid var(--border);
      border-radius:var(--radius-1);
      background:var(--bg-muted);
      font-family:var(--font-mono);
      font-size:.8rem;
      white-space:pre-wrap;
      word-break:break-word;
    }
    .muted { color:var(--text-3); font-size:.85rem; }
  `]
})
export class TaskLogsModalComponent implements AfterViewChecked {
  private pendingScroll = false;
  private readonly expandedIds = new Set<string>();

  @ViewChild('logContainer') private logContainer?: ElementRef<HTMLDivElement>;

  private _visible = false;
  @Input() set visible(value: boolean) {
    this._visible = value;
    this.scheduleScroll();
  }
  get visible(): boolean {
    return this._visible;
  }

  private _logs: TaskExecutionLogDto[] = [];
  @Input() set logs(value: TaskExecutionLogDto[]) {
    this._logs = value;
    this.scheduleScroll();
  }
  get logs(): TaskExecutionLogDto[] {
    return this._logs;
  }

  @Input() taskName = '';
  @Input() loading = false;
  @Input() error = '';
  @Output() close = new EventEmitter<void>();

  newestFirst = false;

  ngAfterViewChecked(): void {
    if (!this.pendingScroll || !this.visible) return;
    const container = this.logContainer?.nativeElement;
    if (!container) return;

    container.scrollTop = this.newestFirst ? 0 : container.scrollHeight;
    this.pendingScroll = false;
  }

  orderedLogs(): TaskExecutionLogDto[] {
    return this.newestFirst ? [...this.logs].reverse() : this.logs;
  }

  toggleNewestFirst(): void {
    this.newestFirst = !this.newestFirst;
    this.scheduleScroll();
  }

  toggleDetails(logId: string): void {
    if (this.expandedIds.has(logId)) {
      this.expandedIds.delete(logId);
      return;
    }

    this.expandedIds.add(logId);
    this.scheduleScroll();
  }

  isExpanded(logId: string): boolean {
    return this.expandedIds.has(logId);
  }

  formatTimestamp(value: string): string {
    return new Date(value).toLocaleString();
  }

  private scheduleScroll(): void {
    this.pendingScroll = true;
  }
}