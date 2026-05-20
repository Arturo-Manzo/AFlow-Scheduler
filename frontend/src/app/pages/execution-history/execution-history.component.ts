import { Component, DestroyRef, HostListener, OnInit, inject, signal, computed } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { ButtonDirective } from 'ui-design-system';
import { ExecutionService } from '../../services/execution.service';
import { ExecutionDto } from '../../models/models';
import { StatusBadgeComponent } from '../../components/status-badge/status-badge.component';
import { detectUserTimeZone, formatUtcInTimeZone } from '../../shared/timezone-utils';

@Component({
  selector: 'app-execution-history',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, StatusBadgeComponent, ButtonDirective],
  templateUrl: './execution-history.component.html',
  styles: [`
    .filter-field { display:flex; flex-direction:column; gap:.35rem; border:1px solid var(--border); background:var(--bg-surface); padding:.6rem .75rem; min-width:180px; }
    .filter-field.filter-status { min-width:240px; }
    .filter-field label { font-size:.72rem; font-weight:700; color:var(--text-3); text-transform:uppercase; letter-spacing:.05em; }
    .filter-field input,
    .filter-field select { border:none; outline:none; background:transparent; font-size:.9rem; width:100%; color:var(--text-1); }
    .filter-row { display:flex; flex-wrap:wrap; gap:1rem; align-items:flex-end; }
    .panel-header { align-items:center; }
    .panel-toolbar.toolbar-inline { align-items:center; }
    .status-checkboxes { display:flex; gap:.75rem; flex-wrap:wrap; padding-top:.25rem; }
    .checkbox-label { display:flex; align-items:center; gap:.3rem; font-size:.82rem; cursor:pointer; white-space:nowrap; }
    .checkbox-label input[type="checkbox"] { margin:0; }
    .id-cell { color:var(--text-3); font-size:.8rem; }
    .box-cell { font-size:.85rem; color:var(--text-2); }
    .row-selected { background:var(--primary-bg); }
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
    @media (max-width:840px) {
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
