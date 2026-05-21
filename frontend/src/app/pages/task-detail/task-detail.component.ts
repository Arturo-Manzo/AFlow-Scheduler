import { Component, DestroyRef, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonDirective } from 'ui-design-system';
import { forkJoin } from 'rxjs';
import { catchError, of } from 'rxjs';
import {
  BoxDto,
  ExecutionDto,
  ForceStartTaskRequest,
  TaskDto,
  UpdateTaskRequest
} from '../../models/models';
import { StatusBadgeComponent } from '../../components/status-badge/status-badge.component';
import { AuthService } from '../../services/auth.service';
import { BoxesService } from '../../services/boxes.service';
import { ExecutionService } from '../../services/execution.service';
import { TasksService } from '../../services/tasks.service';
import { formatUtcInTimeZone } from '../../shared/timezone-utils';
import { TranslatePipe } from '../../shared/translate.pipe';

@Component({
  selector: 'app-task-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, ReactiveFormsModule, StatusBadgeComponent, ButtonDirective, TranslatePipe],
  template: `
    <div class="view-shell">
      <div class="page-header">
        <div>
          <a class="back-link" [routerLink]="['/boxes', boxId()]">← {{ 'Back to {name}' | translate:{ name: box()?.name || ('Box #' + boxId()) } }}</a>
          <h1>{{ task()?.name || ('Task #' + taskId()) }}</h1>
          <div class="page-subtitle">{{ 'Individual task view with execution history, KPIs and management actions.' | translate }}</div>
        </div>
        <div class="page-actions">
          <button class="btn" (click)="reload()">{{ 'Refresh' | translate }}</button>
          @if (auth.isAdmin && task()) {
            <button class="btn" (click)="openEditTask()">{{ 'Edit' | translate }}</button>
            <button class="btn btn-danger" (click)="requestDelete()">{{ 'Delete' | translate }}</button>
          }
          @if ((auth.isAdmin || auth.isOperator) && task()) {
            <button class="btn btn-primary btn-run" (click)="openForceStart()">{{ 'Force Start' | translate }}</button>
          }
        </div>
      </div>

      @if (loading()) {
        <div class="loading-state"><span class="spinner"></span> {{ 'Loading task details...' | translate }}</div>
      } @else if (error()) {
        <div class="alert alert-danger">{{ error() }}</div>
      } @else if (task()) {
        <div class="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
          <article class="ui-card ui-card--padded">
            <div class="flex items-center justify-between">
              <p class="ui-kpi-label">Total Runs</p>
              <svg class="h-5 w-5 text-[var(--color-muted)]" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10"></path>
              </svg>
            </div>
            <p class="ui-kpi-value text-[var(--color-text)]">{{ totalExecutions() }}</p>
          </article>
          <article class="ui-card ui-card--padded">
            <div class="flex items-center justify-between">
              <p class="ui-kpi-label">Success Rate</p>
              <svg class="h-5 w-5 text-[var(--ui-success-text)]" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"></path>
              </svg>
            </div>
            <p class="ui-kpi-value text-[var(--ui-success-text)]">{{ successRate() }}%</p>
          </article>
          <article class="ui-card ui-card--padded">
            <div class="flex items-center justify-between">
              <p class="ui-kpi-label">{{ 'Avg Duration' | translate }}</p>
              <svg class="h-5 w-5 text-[var(--color-muted)]" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"></path>
              </svg>
            </div>
            <p class="ui-kpi-value text-[var(--color-text)]">{{ avgDurationLabel() }}</p>
          </article>
        </div>

        <div class="box-details-panel ui-card ui-card--padded">
          <div class="box-details-table-wrap">
            <table class="ui-table box-details-table">
              <tbody>
                <tr>
                  <th>Type</th>
                  <td>{{ task()!.taskType }}</td>
                </tr>
                <tr>
                  <th>Status</th>
                  <td><span [class]="'badge ' + (task()!.enabled ? 'badge-success' : 'badge-danger')">{{ task()!.enabled ? 'Active' : 'Disabled' }}</span></td>
                </tr>
                <tr>
                  <th>Dependencies</th>
                  <td>{{ dependencyLabel() }}</td>
                </tr>
                <tr>
                  <th>Command</th>
                  <td>{{ task()!.command }}</td>
                </tr>
                <tr>
                  <th>Created</th>
                  <td>{{ formatDate(task()!.createdAt) }}</td>
                </tr>
                <tr>
                  <th>Last Run</th>
                  <td>{{ executions().length ? formatDate(executions()[0].startedAt) : 'Never' }}</td>
                </tr>
                <tr>
                  <th>Box</th>
                  <td><a [routerLink]="['/boxes', boxId()]" class="back-link">{{ box()?.name || ('Box #' + boxId()) }}</a></td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <section class="data-panel">
          <div class="panel-header">
            <div class="panel-title-wrap">
              <div class="panel-title">Recent Executions</div>
              <div class="panel-subtitle">Showing only the 10 most recent executions for this task, from any trigger source.</div>
            </div>
          </div>

          @if (recentExecutions().length === 0) {
            <div class="empty-state"><p>No execution history found for this task.</p></div>
          } @else {
            <div class="panel-body">
              <div class="ui-table-wrap">
                <table class="ui-table">
                  <thead>
                    <tr>
                      <th>#</th>
                      <th>{{ 'Status' | translate }}</th>
                      <th>{{ 'Trigger' | translate }}</th>
                      <th>{{ 'Started At' | translate }}</th>
                      <th>{{ 'Duration' | translate }}</th>
                      <th>Exit Code</th>
                      <th>{{ 'Reason' | translate }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (exec of recentExecutions(); track exec.executionId) {
                      <tr>
                        <td>{{ exec.executionId }}</td>
                        <td><app-status-badge [status]="$any(exec.status)" /></td>
                        <td>{{ exec.triggerSource }}</td>
                        <td>{{ formatDate(exec.startedAt) }}</td>
                        <td>{{ formatDuration(exec.durationSeconds) }}</td>
                        <td>{{ exec.exitCode ?? '--' }}</td>
                        <td>{{ exec.reason || '--' }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }
        </section>
      }
    </div>

    @if (showEditForm()) {
      <div class="modal-overlay modal-overlay-top" role="dialog" aria-modal="true">
        <div class="modal" (click)="$event.stopPropagation()" style="max-width:560px;width:95vw">
          <div class="modal-header">
            <h3>{{ 'Edit Task' | translate }}</h3>
            <button type="button" class="modal-close" (click)="closeEditForm()" [attr.aria-label]="'Close' | translate">x</button>
          </div>
          <form [formGroup]="editForm" (ngSubmit)="saveEdit()" novalidate>
            <div class="modal-body">
              <div class="field">
                <label for="td-name">Name</label>
                <input id="td-name" formControlName="name" [class.is-invalid]="editFieldInvalid('name')" />
                @if (editFieldInvalid('name')) { <span class="field-hint">Name is required.</span> }
              </div>
              <div class="field">
                <label for="td-desc">Description</label>
                <input id="td-desc" formControlName="description" placeholder="Optional" />
              </div>
              <div class="field">
                <label for="td-cmd">Command</label>
                <input id="td-cmd" formControlName="command" [class.is-invalid]="editFieldInvalid('command')" />
                @if (editFieldInvalid('command')) { <span class="field-hint">Command is required.</span> }
              </div>
              <div class="field">
                <label for="td-type">Task Type</label>
                <select id="td-type" formControlName="taskType">
                  @for (opt of taskTypeOptions; track opt.value) {
                    <option [value]="opt.value">{{ opt.label }}</option>
                  }
                </select>
              </div>
              <div class="field field-check">
                <input type="checkbox" formControlName="enabled" id="td-en" />
                <label for="td-en">Enabled</label>
              </div>
              @if (editFormError()) { <div class="alert alert-danger" style="margin-top:.75rem">{{ editFormError() }}</div> }
            </div>
            <div class="modal-footer">
              <button type="button" class="btn" (click)="closeEditForm()">{{ 'Cancel' | translate }}</button>
              <button type="submit" class="btn btn-primary" [disabled]="editSaving()">{{ editSaving() ? ('Saving...' | translate) : ('Save Task' | translate) }}</button>
            </div>
          </form>
        </div>
      </div>
    }

    @if (showDeleteConfirm()) {
      <div class="modal-overlay modal-overlay-top" role="dialog" aria-modal="true">
        <div class="modal" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h3>{{ 'Delete Task' | translate }}</h3>
            <button type="button" class="modal-close" (click)="cancelDelete()" [attr.aria-label]="'Close' | translate">x</button>
          </div>
          <div class="modal-body">
            <p>{{ 'Delete task {name}?' | translate:{ name: task()!.name } }} {{ 'This action cannot be undone.' | translate }}</p>
            @if (deleteError()) { <div class="alert alert-danger">{{ deleteError() }}</div> }
          </div>
          <div class="modal-footer">
            <button class="btn" (click)="cancelDelete()">{{ 'Cancel' | translate }}</button>
            <button class="btn btn-danger" (click)="confirmDelete()" [disabled]="deleteLoading()">
              {{ deleteLoading() ? ('Deleting...' | translate) : ('Confirm Delete' | translate) }}
            </button>
          </div>
        </div>
      </div>
    }

    @if (showForceStart()) {
      <div class="modal-overlay" style="z-index:1200" role="dialog" aria-modal="true">
        <div class="modal" (click)="$event.stopPropagation()" style="max-width:440px;width:95vw">
          <div class="modal-header">
            <h3>{{ 'Force Start Task' | translate }}</h3>
            <button type="button" class="modal-close" (click)="closeForceStart()" [attr.aria-label]="'Close' | translate">x</button>
          </div>
          <form [formGroup]="forceStartForm" (ngSubmit)="confirmForceStart()">
            <div class="modal-body">
              <p>This will execute <strong>{{ task()!.name }}</strong> ignoring its dependencies. Continue?</p>
              <div class="field" style="margin-top:.75rem">
                <label for="fs-reason">{{ 'Reason' | translate }} <span style="color:var(--danger)">*</span></label>
                <input id="fs-reason" formControlName="reason" placeholder="e.g. Manual retry after outage"
                       [class.is-invalid]="forceStartFieldInvalid()" />
                @if (forceStartFieldInvalid()) { <span class="field-hint">{{ 'Reason is required.' | translate }}</span> }
              </div>
              @if (forceStartMessage()) { <div class="alert alert-success">{{ forceStartMessage() }}</div> }
              @if (forceStartError()) { <div class="alert alert-danger">{{ forceStartError() }}</div> }
            </div>
            <div class="modal-footer">
              <button type="button" class="btn" (click)="closeForceStart()">{{ 'Close' | translate }}</button>
              @if (!forceStartMessage()) {
                <button type="submit" class="btn btn-primary" [disabled]="forceStartLoading()">
                  {{ forceStartLoading() ? ('Starting...' | translate) : ('Force Start' | translate) }}
                </button>
              }
            </div>
          </form>
        </div>
      </div>
    }
  `,
  styles: [`
    .back-link { color: var(--text-2); text-decoration: none; font-size: .85rem; }
    .back-link:hover { text-decoration: underline; }
    .page-subtitle { margin-top: .35rem; }
    .inline-code { font-size: .78rem; background: var(--bg-muted); border: 1px solid var(--border); border-radius: 4px; padding: .18rem .42rem; word-break: break-all; display: inline-block; }
    .type-badge { font-size: .68rem; font-weight: 700; text-transform: uppercase; padding: .15rem .4rem; border-radius: 3px; letter-spacing: .04em; }
    .type-exe { background: #e8f4fd; color: #1565c0; }
    .type-bat { background: #fdf3e8; color: #c17a00; }
    .type-python { background: #e8f5e9; color: #2e7d32; }
    .type-api { background: #f3e8fd; color: #6a1b9a; }
    .box-details-panel { margin-bottom: 1.25rem; }
    .box-details-table-wrap { overflow: hidden; border: 1px solid var(--border); border-radius: var(--radius-2); }
    .box-details-table { width: 100%; border-collapse: collapse; }
    .box-details-table th, .box-details-table td { padding: .85rem 1rem; }
    .box-details-table th { width: 32%; text-align: left; vertical-align: top; font-weight: 700; color: var(--text-3); background: var(--bg-muted); }
    .box-details-table td { color: var(--text-1); }
    .box-details-table tr + tr td { border-top: 1px solid var(--border); }
    @media (max-width: 1100px) {
      .metrics-grid { grid-template-columns: 1fr; }
    }
    @media (max-width: 720px) {
      .page-header { flex-direction: column; gap: .75rem; }
    }
  `]
})
export class TaskDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private boxesService = inject(BoxesService);
  private tasksService = inject(TasksService);
  private executionService = inject(ExecutionService);
  readonly auth = inject(AuthService);
  private fb = inject(FormBuilder);
  private destroyRef = inject(DestroyRef);
  private readonly userTimeZone = Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';

  boxId = signal(0);
  taskId = signal(0);
  box = signal<BoxDto | null>(null);
  task = signal<TaskDto | null>(null);
  executions = signal<ExecutionDto[]>([]);
  loading = signal(true);
  error = signal('');

  // Edit form
  showEditForm = signal(false);
  editFormError = signal('');
  editSaving = signal(false);
  showDeleteConfirm = signal(false);
  deleteLoading = signal(false);
  deleteError = signal('');

  // Force start form
  showForceStart = signal(false);
  forceStartLoading = signal(false);
  forceStartMessage = signal('');
  forceStartError = signal('');

  readonly taskTypeOptions = [
    { value: 'Exe', label: 'Exe (.exe process)' },
    { value: 'Bat', label: 'Bat (batch script)' },
    { value: 'Python', label: 'Python script' },
    { value: 'Api', label: 'Api (HTTP request)' }
  ];

  editForm = this.fb.group({
    name: ['', Validators.required],
    description: [''],
    command: ['', Validators.required],
    taskType: ['Exe', Validators.required],
    enabled: [true]
  });

  forceStartForm = this.fb.group({
    reason: ['', Validators.required]
  });

  readonly totalExecutions = computed(() => this.executions().length);
  readonly recentExecutions = computed(() => this.executions().slice(0, 10));

  readonly successRate = computed(() => {
    const all = this.executions();
    if (!all.length) return 0;
    const success = all.filter(e => e.status === 'Success').length;
    return Math.round((success / all.length) * 100);
  });

  readonly avgDurationLabel = computed(() => {
    const completed = this.executions().filter(e => e.durationSeconds != null);
    if (!completed.length) return '--';
    const avg = completed.reduce((sum, e) => sum + (e.durationSeconds ?? 0), 0) / completed.length;
    return this.formatDuration(Math.round(avg));
  });

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.showForceStart()) { this.closeForceStart(); return; }
    if (this.showEditForm()) { this.closeEditForm(); return; }
    if (this.showDeleteConfirm()) { this.cancelDelete(); }
  }

  ngOnInit(): void {
    this.boxId.set(Number(this.route.snapshot.paramMap.get('boxId')) || 0);
    this.taskId.set(Number(this.route.snapshot.paramMap.get('taskId')) || 0);
    this.reload();
  }

  reload(): void {
    const bId = this.boxId();
    const tId = this.taskId();
    if (!bId || !tId) {
      this.error.set('Invalid route parameters.');
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.error.set('');

    forkJoin({
      box: this.boxesService.getById(bId),
      task: this.tasksService.getById(tId),
      executions: this.executionService.getExecutionsForTask(tId).pipe(catchError(() => of([])))
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: ({ box, task, executions }) => {
        this.box.set(box);
        this.task.set(task);
        const sorted = [...executions].sort(
          (a, b) => new Date(b.startedAt).getTime() - new Date(a.startedAt).getTime()
        );
        this.executions.set(sorted);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load task details.');
        this.loading.set(false);
      }
    });
  }

  dependencyLabel(): string {
    const t = this.task();
    if (!t) return '--';
    if (!t.dependencyTaskIds?.length) return 'None';
    const box = this.box();
    if (!box) return `${t.dependencyTaskIds.length} task(s)`;
    return t.dependencyTaskIds
      .map(id => box.tasks.find(bt => bt.taskId === id)?.name ?? `#${id}`)
      .join(', ');
  }

  formatDate(value: string | null | undefined): string {
    return formatUtcInTimeZone(value, this.userTimeZone, { dateStyle: 'short', timeStyle: 'short' });
  }

  formatDuration(value?: number | null): string {
    if (value == null) return '--';
    if (value < 60) return `${value}s`;
    const hours = Math.floor(value / 3600);
    const minutes = Math.floor((value % 3600) / 60);
    const seconds = value % 60;
    if (hours > 0) return `${hours}h ${minutes}m ${seconds}s`;
    return `${minutes}m ${seconds}s`;
  }

  // --- Edit ---
  openEditTask(): void {
    const t = this.task()!;
    this.editForm.patchValue({
      name: t.name,
      description: t.description,
      command: t.command,
      taskType: t.taskType,
      enabled: t.enabled
    });
    this.editFormError.set('');
    this.showEditForm.set(true);
  }

  closeEditForm(): void {
    this.showEditForm.set(false);
  }

  editFieldInvalid(field: string): boolean {
    const control = this.editForm.get(field)!;
    return control.invalid && (control.dirty || control.touched);
  }

  saveEdit(): void {
    this.editForm.markAllAsTouched();
    if (this.editForm.invalid) return;
    const v = this.editForm.value;
    const request: UpdateTaskRequest = {
      name: v.name!,
      description: v.description ?? '',
      command: v.command!,
      taskType: v.taskType || 'Exe',
      enabled: v.enabled ?? true,
      dependencyTaskIds: this.task()?.dependencyTaskIds ?? []
    };
    this.editSaving.set(true);
    this.editFormError.set('');
    this.tasksService.update(this.taskId(), request).subscribe({
      next: () => {
        this.editSaving.set(false);
        this.closeEditForm();
        this.reload();
      },
      error: (err) => {
        this.editFormError.set(err?.error?.message || 'Failed to save task.');
        this.editSaving.set(false);
      }
    });
  }

  // --- Delete ---
  requestDelete(): void {
    this.deleteError.set('');
    this.showDeleteConfirm.set(true);
  }

  cancelDelete(): void {
    this.showDeleteConfirm.set(false);
  }

  confirmDelete(): void {
    this.deleteLoading.set(true);
    this.deleteError.set('');
    this.tasksService.delete(this.taskId()).subscribe({
      next: () => {
        // Navigate back to box detail after deletion
        window.history.back();
      },
      error: (err) => {
        this.deleteError.set(err?.error?.message || 'Failed to delete task.');
        this.deleteLoading.set(false);
      }
    });
  }

  // --- Force Start ---
  openForceStart(): void {
    this.forceStartForm.reset();
    this.forceStartMessage.set('');
    this.forceStartError.set('');
    this.showForceStart.set(true);
  }

  closeForceStart(): void {
    this.showForceStart.set(false);
  }

  forceStartFieldInvalid(): boolean {
    const c = this.forceStartForm.get('reason')!;
    return c.invalid && (c.dirty || c.touched);
  }

  confirmForceStart(): void {
    this.forceStartForm.markAllAsTouched();
    if (this.forceStartForm.invalid) return;
    this.forceStartLoading.set(true);
    this.forceStartError.set('');
    const request: ForceStartTaskRequest = { reason: this.forceStartForm.value.reason! };
    this.tasksService.forceStart(this.taskId(), request).subscribe({
      next: (res) => {
        this.forceStartLoading.set(false);
        if (res.success) {
          this.forceStartMessage.set('Task queued for execution.');
          this.reload();
        } else {
          this.forceStartError.set(res.message || 'Request failed.');
        }
      },
      error: (err) => {
        this.forceStartLoading.set(false);
        this.forceStartError.set(err?.error?.message || 'Failed to start task.');
      }
    });
  }
}
