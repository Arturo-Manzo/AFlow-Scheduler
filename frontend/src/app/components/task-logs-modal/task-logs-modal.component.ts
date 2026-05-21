import { AfterViewChecked, Component, ElementRef, EventEmitter, Input, Output, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TaskExecutionLogDto } from '../../models/models';
import { ButtonDirective } from 'ui-design-system';
import { TranslatePipe } from '../../shared/translate.pipe';

@Component({
  selector: 'app-task-logs-modal',
  standalone: true,
  imports: [CommonModule, ButtonDirective, TranslatePipe],
  templateUrl: './task-logs-modal.component.html',
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
