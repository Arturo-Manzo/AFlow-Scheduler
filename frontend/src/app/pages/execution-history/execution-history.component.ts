import { Component, DestroyRef, HostListener, OnInit, inject, signal, computed } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { ExecutionService } from '../../services/execution.service';
import { ExecutionDto } from '../../models/models';
import { StatusBadgeComponent } from '../../components/status-badge/status-badge.component';
import { detectUserTimeZone, formatUtcInTimeZone } from '../../shared/timezone-utils';

@Component({
  selector: 'app-execution-history',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, StatusBadgeComponent],
  template: `
    <div class="view-shell">
      <div class="view-hero">
        <div class="view-hero-main">
          <div class="view-eyebrow">Execution Audit</div>
          <h1>Execution History</h1>
          <p class="view-description">
            View task executions filtered by status, date range and more. All times shown in {{ userTimeZone }}.
          </p>
        </div>
        <div class="view-hero-kpi">
          <span class="kpi-value">{{ loading() ? '--' : executions().length }}</span>
          <span class="kpi-label">Loaded Records</span>
        </div>
        <div class="view-hero-kpi">
          <span class="kpi-value">{{ loading() ? '--' : failedCount() }}</span>
          <span class="kpi-label">Failed</span>
        </div>
        <div class="view-hero-kpi">
          <span class="kpi-value">{{ loading() ? '--' : abortedCount() }}</span>
          <span class="kpi-label">Aborted</span>
        </div>
        <div class="view-hero-kpi">
          <span class="kpi-value">{{ loading() ? '--' : skippedCount() }}</span>
          <span class="kpi-label">Skipped</span>
        </div>
        <div class="view-hero-kpi">
          <span class="kpi-value">{{ loading() ? '--' : successCount() }}</span>
          <span class="kpi-label">Success</span>
        </div>
      </div>

      <!-- Filters -->
      <section class="data-panel">
        <div class="panel-header">
          <div class="panel-title-wrap">
            <div class="panel-title">Filters</div>
          </div>
        </div>
        <div class="panel-body filter-row">
          <div class="field">
            <label for="f-limit">Show</label>
            <select id="f-limit" [value]="limit()" (change)="onLimitChange($event)">
              <option [value]="25">25</option>
              <option [value]="50">50</option>
              <option [value]="100">100</option>
              <option [value]="200">200</option>
            </select>
          </div>
          <div class="field">
            <label>Status</label>
            <div class="status-checkboxes">
              @for (s of allStatuses; track s) {
                <label class="checkbox-label">
                  <input type="checkbox"
                    [checked]="selectedStatuses().includes(s)"
                    (change)="onStatusToggle(s, $event)" />
                  {{ s }}
                </label>
              }
            </div>
          </div>
          <div class="field">
            <label for="f-from">From (Local)</label>
            <input id="f-from" type="datetime-local" [value]="fromLocal()" (change)="onFromChange($event)" />
          </div>
          <div class="field">
            <label for="f-to">To (Local)</label>
            <input id="f-to" type="datetime-local" [value]="toLocal()" (change)="onToChange($event)" />
          </div>
          <div class="filter-actions">
            <button class="btn btn-primary" (click)="applyFilters()" [disabled]="loading()">Apply</button>
            <button class="btn" (click)="clearFilters()" [disabled]="loading()">Clear</button>
            <button class="btn" (click)="reload()" [disabled]="loading()">Refresh</button>
          </div>
        </div>
      </section>

      <!-- Table -->
      <section class="data-panel">
        <div class="panel-header">
          <div class="panel-title-wrap">
            <div class="panel-title">Execution Records</div>
            <div class="panel-subtitle">Click a row to inspect stdout, stderr and error details.</div>
          </div>
        </div>

        @if (loading()) {
          <div class="loading-state"><span class="spinner"></span> Loading executions...</div>
        } @else if (loadError()) {
          <div class="panel-body"><div class="alert alert-danger">{{ loadError() }}</div></div>
        } @else if (executions().length === 0) {
          <p class="empty-state">No executions found for the selected filters.</p>
        } @else {
          <table class="data-table">
            <thead>
              <tr>
                <th>#</th>
                <th>Task</th>
                <th>Box</th>
                <th>Status</th>
                <th>Source</th>
                <th>Started</th>
                <th>Duration</th>
                <th>Exit</th>
              </tr>
            </thead>
            <tbody>
              @for (exec of executions(); track exec.executionId) {
                <tr
                  (click)="select(exec)"
                  [class.row-selected]="selected()?.executionId === exec.executionId"
                  style="cursor:pointer"
                >
                  <td class="id-cell">{{ exec.executionId }}</td>
                  <td><strong>{{ exec.taskName }}</strong></td>
                  <td class="box-cell">{{ exec.boxName || '--' }}</td>
                  <td><app-status-badge [status]="exec.status" /></td>
                  <td><span class="badge badge-neutral">{{ exec.triggerSource }}</span></td>
                  <td>{{ formatTime(exec.startedAt) }}</td>
                  <td>{{ exec.durationSeconds != null ? exec.durationSeconds + 's' : '--' }}</td>
                  <td>{{ exec.exitCode ?? '--' }}</td>
                </tr>
              }
            </tbody>
          </table>
        }
      </section>

      <!-- Detail modal -->
      @if (selected()) {
        <div class="modal-overlay" role="dialog" aria-modal="true" (click)="selected.set(null)">
          <div class="modal" (click)="$event.stopPropagation()" style="max-width:760px;width:95vw;max-height:90vh;overflow-y:auto">
            <div class="modal-header">
              <div class="modal-header-main">
                <div class="detail-heading-line">
                  <h3 class="detail-title">{{ selected()!.taskName }}</h3>
                  <app-status-badge [status]="selected()!.status" />
                </div>
                <div class="detail-meta">
                  <span class="detail-meta-label">Box</span>
                  <strong>{{ selected()!.boxName || '--' }}</strong>
                  <span class="detail-meta-sep">·</span>
                  <span class="detail-meta-label">Trigger</span>
                  <span class="badge badge-neutral" style="font-size:.7rem">{{ selected()!.triggerSource }}</span>
                </div>
              </div>
              <button type="button" class="modal-close" (click)="selected.set(null)" aria-label="Close">x</button>
            </div>
            <div class="modal-body">
              <table class="detail-table">
                <tbody>
                  <tr><th>Started</th><td>{{ formatTime(selected()!.startedAt, 'medium') }}</td></tr>
                  <tr><th>Ended</th><td>{{ selected()!.endedAt ? formatTime(selected()!.endedAt!, 'medium') : '--' }}</td></tr>
                  <tr><th>Task Type</th><td>{{ selected()!.taskType || '--' }}</td></tr>
                  <tr><th>Duration</th><td>{{ selected()!.durationSeconds != null ? selected()!.durationSeconds + 's' : '--' }}</td></tr>
                  <tr><th>Exit Code</th><td>{{ selected()!.exitCode ?? '--' }}</td></tr>
                  <tr><th>Department</th><td>{{ selected()!.departmentName || 'Not assigned' }}</td></tr>
                  <tr><th>Failure Alert Email</th><td>{{ selected()!.failureAlertEmail || 'Not configured' }}</td></tr>
                  @if (selected()!.requestedByUsername) {
                    <tr><th>Requested By</th><td>{{ selected()!.requestedByUsername }}</td></tr>
                  }
                  @if (selected()!.reason) {
                    <tr><th>Reason</th><td>{{ selected()!.reason }}</td></tr>
                  }
                </tbody>
              </table>
              <div class="detail-section">
                <p class="section-title" style="margin-top:0">Command</p>
                <pre>{{ selected()!.command || '(empty)' }}</pre>
              </div>
              <div class="detail-section">
                <p class="section-title">Standard Output</p>
                <pre>{{ selected()!.stdOut || '(empty)' }}</pre>
              </div>
              <div class="detail-section">
                <p class="section-title">Standard Error</p>
                <pre class="pre-err">{{ selected()!.stdErr || '(empty)' }}</pre>
              </div>
            </div>
            <div class="modal-footer">
              <button class="btn" (click)="selected.set(null)">Close</button>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .filter-row { display:flex; gap:1rem; align-items:flex-end; flex-wrap:wrap; }
    .filter-row .field { margin:0; min-width:130px; }
    .filter-actions { display:flex; flex-direction:row; gap:.5rem; align-items:flex-end; align-self:flex-end; }
    .status-checkboxes { display:flex; gap:.75rem; flex-wrap:wrap; padding-top:.25rem; }
    .checkbox-label { display:flex; align-items:center; gap:.3rem; font-size:.82rem; cursor:pointer; white-space:nowrap; }
    .checkbox-label input[type="checkbox"] { margin:0; }
    .id-cell { color:var(--text-3); font-size:.8rem; }
    .box-cell { font-size:.85rem; color:var(--text-2); }
    .row-selected { background:color-mix(in srgb, var(--danger, #ef4444) 6%, white); }
    .detail-title { margin: 0; font-size: 1.05rem; font-weight: 800; color: var(--text-1); }
    .detail-heading-line { display:flex; align-items:center; gap:.55rem; flex-wrap:wrap; }
    .detail-meta {
      display:flex;
      align-items:center;
      gap:.4rem;
      margin-top:.4rem;
      color:var(--text-2);
      font-size:.8rem;
      flex-wrap:wrap;
    }
    .detail-meta-label { font-size:.68rem; letter-spacing:.05em; text-transform:uppercase; color:var(--text-3); font-weight:700; }
    .detail-meta-sep { color: var(--text-3); }
    .detail-section { padding:1rem 0; }
    pre { background:var(--bg-muted); border:1px solid var(--border); border-radius:var(--radius-1); padding:.75rem 1rem; font-family:var(--font-mono); font-size:.8rem; overflow-x:auto; max-height:180px; white-space:pre-wrap; word-break:break-all; color:var(--text-1); }
    pre.pre-err { color:var(--danger); }
    .detail-table { width:100%; border-collapse:collapse; margin-bottom:.9rem; }
    .detail-table th,
    .detail-table td { border:1px solid var(--border); padding:.55rem .7rem; text-align:left; vertical-align:top; }
    .detail-table th { width:190px; background:var(--bg-muted); font-size:.74rem; letter-spacing:.04em; text-transform:uppercase; color:var(--text-2); }
    @media (max-width:640px) {
      .filter-row { flex-direction:column; }
      .detail-table th,
      .detail-table td { display:block; width:100%; }
      .detail-table th { border-bottom:none; }
    }
  `]
})
export class ExecutionHistoryComponent implements OnInit {
  private executionService = inject(ExecutionService);
  private destroyRef = inject(DestroyRef);

  readonly userTimeZone = detectUserTimeZone();
  readonly allStatuses = ['Success', 'Failed', 'Aborted', 'Skipped', 'Running'] as const;

  executions = signal<ExecutionDto[]>([]);
  loading = signal(true);
  loadError = signal('');
  selected = signal<ExecutionDto | null>(null);

  // Filters
  limit = signal(50);
  fromLocal = signal('');
  toLocal = signal('');
  selectedStatuses = signal<string[]>(['Failed', 'Aborted', 'Skipped']);

  failedCount = computed(() => this.executions().filter(e => e.status === 'Failed').length);
  abortedCount = computed(() => this.executions().filter(e => e.status === 'Aborted').length);
  skippedCount = computed(() => this.executions().filter(e => e.status === 'Skipped').length);
  successCount = computed(() => this.executions().filter(e => e.status === 'Success').length);

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.selected()) { this.selected.set(null); }
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set('');
    this.executionService.getFailed({
      limit: this.limit(),
      fromUtc: this.toUtcIso(this.fromLocal()),
      toUtc: this.toUtcIso(this.toLocal()),
      status: this.selectedStatuses().length > 0 ? this.selectedStatuses() : undefined
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: data => { this.executions.set(data); this.loading.set(false); },
      error: (err) => { this.loadError.set(err?.error?.message || 'Failed to load executions.'); this.loading.set(false); }
    });
  }

  reload(): void { this.load(); }

  applyFilters(): void { this.load(); }

  clearFilters(): void {
    this.limit.set(50);
    this.fromLocal.set('');
    this.toLocal.set('');
    this.selectedStatuses.set(['Failed', 'Aborted', 'Skipped']);
    this.load();
  }

  onLimitChange(event: Event): void {
    this.limit.set(Number((event.target as HTMLSelectElement).value));
  }

  onStatusToggle(status: string, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    const current = this.selectedStatuses();
    if (checked) {
      this.selectedStatuses.set([...current, status]);
    } else {
      this.selectedStatuses.set(current.filter(s => s !== status));
    }
  }

  onFromChange(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.fromLocal.set(val || '');
  }

  onToChange(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.toLocal.set(val || '');
  }

  select(exec: ExecutionDto): void {
    this.selected.set(this.selected()?.executionId === exec.executionId ? null : exec);
  }

  formatTime(utc: string, format: 'short' | 'medium' = 'short'): string {
    const opts: Intl.DateTimeFormatOptions = format === 'medium'
      ? { dateStyle: 'medium', timeStyle: 'medium' }
      : { dateStyle: 'short', timeStyle: 'short' };
    return formatUtcInTimeZone(utc, this.userTimeZone, opts);
  }

  private toUtcIso(localDateTime: string): string | undefined {
    if (!localDateTime) return undefined;
    const parsed = new Date(localDateTime);
    if (Number.isNaN(parsed.getTime())) return undefined;
    return parsed.toISOString();
  }
}
