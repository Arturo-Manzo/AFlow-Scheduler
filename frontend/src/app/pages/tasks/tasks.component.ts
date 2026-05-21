import { Component, HostListener, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { BoxesService } from '../../services/boxes.service';
import { DepartmentsService } from '../../services/departments.service';
import { SearchService } from '../../services/search.service';
import { TasksService } from '../../services/tasks.service';
import { AuthService } from '../../services/auth.service';
import { BoxDto, CreateBoxRequest, UpdateBoxRequest, ExecuteBoxRequest, TaskDto, SearchResultDto, SearchScope, CreateTaskRequest, UpdateTaskRequest, ForceStartTaskRequest, DepartmentDto } from '../../models/models';
import { detectUserTimeZone, formatUtcShorthand, formatUtcWithBoxContextShorthand, getAvailableTimeZones, FrequencyOption, parseCronToSchedule, describeCron as sharedDescribeCron } from '../../shared/timezone-utils';
import { isFieldInvalid } from '../../shared/form-utils';
import { HighlightPipe } from '../../shared/highlight.pipe';
import { ButtonDirective } from 'ui-design-system';
import { TranslatePipe } from '../../shared/translate.pipe';

function notificationEmailListValidator(control: AbstractControl): ValidationErrors | null {
  const rawValue = `${control.value ?? ''}`.trim();
  if (!rawValue) {
    return null;
  }

  const recipients = rawValue
    .replace(/;/g, ',')
    .split(',')
    .map((item) => item.trim())
    .filter((item) => item.length > 0);

  if (recipients.length === 0 || recipients.length > 10) {
    return { emailList: true };
  }

  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  return recipients.every((item) => emailRegex.test(item)) ? null : { emailList: true };
}

function normalizeNotificationEmails(rawValue: unknown): string | undefined {
  const text = `${rawValue ?? ''}`.trim();
  if (!text) {
    return undefined;
  }

  const normalized = text
    .replace(/;/g, ',')
    .split(',')
    .map((item) => item.trim())
    .filter((item) => item.length > 0)
    .join(', ');

  return normalized || undefined;
}

@Component({
  selector: 'app-tasks',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, HighlightPipe, ButtonDirective, TranslatePipe],
  templateUrl: './tasks.component.html',
  styles: [`
    .type-badge { font-size:.68rem;font-weight:700;text-transform:uppercase;padding:.15rem .4rem;border-radius:3px;letter-spacing:.04em }
    .type-exe { background:#e8f4fd;color:#1565c0 }
    .type-bat { background:#fdf3e8;color:#c17a00 }
    .type-python { background:#e8f5e9;color:#2e7d32 }
    .type-api { background:#f3e8fd;color:#6a1b9a }
    :host ::ng-deep mark.search-hit { background:#fff3a3;color:#1f2937;padding:0 .12rem;border-radius:3px }
    @media (max-width: 920px) {
      .grid { grid-template-columns:1fr }
    }
  `]
})
export class TasksComponent implements OnInit, OnDestroy {
  private boxesService = inject(BoxesService);
  private departmentsService = inject(DepartmentsService);
  private searchService = inject(SearchService);
  private tasksService = inject(TasksService);
  private router = inject(Router);
  auth = inject(AuthService);
  private fb = inject(FormBuilder);

  readonly userTimeZone = detectUserTimeZone();
  readonly availableTimeZones = getAvailableTimeZones(this.userTimeZone);
  private taskSearchSubscription?: Subscription;
  private lastExecutedSearch = '';

  // --- Box list ---
  boxes = signal<BoxDto[]>([]);
  departments = signal<DepartmentDto[]>([]);
  loading = signal(true);
  saving = signal(false);
  deleteLoading = signal(false);
  loadError = signal('');
  showBoxForm = signal(false);
  runFormVisible = signal(false);
  editingBox = signal<BoxDto | null>(null);
  runningBox = signal<BoxDto | null>(null);
  boxPendingDelete = signal<BoxDto | null>(null);
  boxFormError = signal('');
  deleteError = signal('');
  runMessage = signal('');
  runError = signal('');

  // --- Global task search ---
  taskSearchResults = signal<SearchResultDto[]>([]);
  taskSearchLoading = signal(false);
  taskSearchError = signal('');
  taskSearchPerformed = signal(false);
  taskSearchQuery = signal('');

  // --- Task force start ---
  forceStartPendingTask = signal<TaskDto | null>(null);
  forceStartLoading = signal(false);
  forceStartMessage = signal('');
  forceStartError = signal('');

  // --- Detail view ---
  viewingBox = signal<BoxDto | null>(null);
  loadingDetail = signal(false);
  detailError = signal('');

  // --- Task form ---
  showTaskForm = signal(false);
  editingTask = signal<TaskDto | null>(null);
  taskFormError = signal('');
  taskSaving = signal(false);
  taskPendingDelete = signal<TaskDto | null>(null);
  taskDeleteLoading = signal(false);
  taskDeleteError = signal('');

  readonly dayOptions = [
    { key: 'dayMon', label: 'Mon', dow: 1 },
    { key: 'dayTue', label: 'Tue', dow: 2 },
    { key: 'dayWed', label: 'Wed', dow: 3 },
    { key: 'dayThu', label: 'Thu', dow: 4 },
    { key: 'dayFri', label: 'Fri', dow: 5 },
    { key: 'daySat', label: 'Sat', dow: 6 },
    { key: 'daySun', label: 'Sun', dow: 0 }
  ] as const;

  readonly taskTypeOptions = [
    { value: 'Exe', label: 'Exe (.exe process)' },
    { value: 'Bat', label: 'Bat (batch script)' },
    { value: 'Python', label: 'Python script' },
    { value: 'Api', label: 'Api (HTTP request)' }
  ];

  boxForm = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    description: [''],
    notificationEmail: ['', notificationEmailListValidator],
    frequency: ['hourly' as FrequencyOption],
    specificTime: ['07:00'],
    timeZoneId: [this.userTimeZone, Validators.required],
    dayMon: [true], dayTue: [true], dayWed: [true], dayThu: [true],
    dayFri: [true], daySat: [false], daySun: [false],
    enabled: [true],
    departmentId: [this.auth.currentUser()?.departmentId ?? null],
    // First task fields — validators applied dynamically in openCreate()
    taskName: [''],
    taskDescription: [''],
    taskCommand: [''],
    taskType: ['Exe']
  });

  taskForm = this.fb.group({
    name: ['', Validators.required],
    description: [''],
    command: ['', Validators.required],
    taskType: ['Exe', Validators.required],
    dependencyTaskIds: [[] as number[]],
    enabled: [true]
  });

  runFormGroup = this.fb.group({
    ignoreDependencies: [false],
    reason: ['', Validators.required]
  });

  forceStartForm = this.fb.group({
    reason: ['', Validators.required]
  });

  taskSearchForm = this.fb.group({
    query: ['', [Validators.required, Validators.minLength(2)]],
    scope: ['all' as SearchScope, Validators.required],
    limit: [25, Validators.required]
  });

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.forceStartPendingTask()) { this.closeForceStart(); return; }
    if (this.showTaskForm()) { this.closeTaskForm(); return; }
    if (this.taskPendingDelete()) { this.cancelDeleteTask(); return; }
    if (this.showBoxForm()) { this.closeBoxForm(); return; }
    if (this.runFormVisible()) { this.closeRunForm(); return; }
    if (this.boxPendingDelete()) { this.cancelDelete(); return; }
    if (this.viewingBox()) { this.closeDetail(); return; }
  }

  @HostListener('document:keydown', ['$event'])
  onGlobalKeydown(event: KeyboardEvent): void {
    const target = event.target as HTMLElement | null;
    const isTypingTarget = !!target && (
      target.tagName === 'INPUT' ||
      target.tagName === 'TEXTAREA' ||
      target.tagName === 'SELECT' ||
      target.isContentEditable
    );

    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
      event.preventDefault();
      this.focusSearchInput();
      return;
    }

    if (!isTypingTarget && event.key === '/') {
      event.preventDefault();
      this.focusSearchInput();
    }
  }

  ngOnInit(): void {
    this.loadBoxes();
    this.loadDepartments();
    this.taskSearchSubscription = this.taskSearchForm.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged((left, right) => JSON.stringify(left) === JSON.stringify(right))
      )
      .subscribe(() => this.runTaskSearch(false));
  }

  ngOnDestroy(): void {
    this.taskSearchSubscription?.unsubscribe();
  }

  bfi(field: string): boolean {
    return isFieldInvalid(this.boxForm, field);
  }

  bft(field: string): boolean {
    return isFieldInvalid(this.taskForm, field);
  }

  bfr(field: string): boolean {
    return isFieldInvalid(this.runFormGroup, field);
  }

  bffs(): boolean {
    return isFieldInvalid(this.forceStartForm, 'reason');
  }

  taskSearchFieldInvalid(): boolean {
    return isFieldInvalid(this.taskSearchForm, 'query');
  }

  private runTaskSearch(markTouched: boolean): void {
    if (markTouched) {
      this.taskSearchForm.markAllAsTouched();
    }

    const query = this.taskSearchForm.value.query?.trim() ?? '';
    const scope = (this.taskSearchForm.value.scope ?? 'all') as SearchScope;
    const limit = Number(this.taskSearchForm.value.limit ?? 25);

    this.taskSearchQuery.set(query);

    if (query.length === 0) {
      this.lastExecutedSearch = '';
      this.taskSearchResults.set([]);
      this.taskSearchError.set('');
      this.taskSearchPerformed.set(false);
      this.taskSearchLoading.set(false);
      return;
    }

    if (query.length < 2) {
      this.taskSearchResults.set([]);
      this.taskSearchError.set(markTouched ? 'Enter at least 2 characters.' : '');
      this.taskSearchPerformed.set(false);
      this.taskSearchLoading.set(false);
      return;
    }

    const searchKey = `${query}::${scope}::${limit}`;
    if (searchKey === this.lastExecutedSearch && !markTouched) {
      return;
    }

    this.lastExecutedSearch = searchKey;
    this.taskSearchLoading.set(true);
    this.taskSearchError.set('');
    this.taskSearchPerformed.set(true);

    this.searchService.search(query, scope, limit).subscribe({
      next: (results) => {
        this.taskSearchResults.set(results);
        this.taskSearchLoading.set(false);
      },
      error: (err) => {
        this.taskSearchResults.set([]);
        this.taskSearchError.set(err?.error?.message || 'Failed to search.');
        this.taskSearchLoading.set(false);
      }
    });
  }

  clearTaskSearch(): void {
    this.taskSearchForm.reset({ query: '', scope: 'all', limit: 25 });
    this.lastExecutedSearch = '';
    this.taskSearchResults.set([]);
    this.taskSearchError.set('');
    this.taskSearchPerformed.set(false);
    this.taskSearchQuery.set('');
  }

  focusSearchInput(): void {
    const input = document.getElementById('task-search-query') as HTMLInputElement | null;
    input?.focus();
    input?.select();
    input?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }

  loadBoxes(): void {
    this.loading.set(true);
    this.loadError.set('');
    this.boxesService.getAll().subscribe({
      next: (bs) => { this.boxes.set(bs); this.loading.set(false); },
      error: () => { this.loadError.set('Failed to load boxes.'); this.loading.set(false); }
    });
  }

  loadDepartments(): void {
    this.departmentsService.getAll().subscribe({
      next: (depts) => this.departments.set(depts),
      error: () => this.departments.set([])
    });
  }

  openCreate(): void {
    this.editingBox.set(null);
    this.boxForm.reset({
      frequency: 'hourly', specificTime: '07:00',
      timeZoneId: this.userTimeZone,
      dayMon: true, dayTue: true, dayWed: true, dayThu: true, dayFri: true, daySat: false, daySun: false,
      enabled: true,
      departmentId: this.auth.currentUser()?.departmentId ?? null,
      taskName: '', taskDescription: '', taskCommand: '', taskType: 'Exe'
    });
    this.boxForm.get('taskName')!.setValidators(Validators.required);
    this.boxForm.get('taskCommand')!.setValidators(Validators.required);
    this.boxForm.get('taskName')!.updateValueAndValidity();
    this.boxForm.get('taskCommand')!.updateValueAndValidity();
    this.boxFormError.set('');
    this.showBoxForm.set(true);
  }

  openEdit(box: BoxDto): void {
    this.editingBox.set(box);
    this.boxForm.get('taskName')!.clearValidators();
    this.boxForm.get('taskCommand')!.clearValidators();
    this.boxForm.get('taskName')!.updateValueAndValidity();
    this.boxForm.get('taskCommand')!.updateValueAndValidity();
    const parsed = this.parseCronToSchedule(box.cronExpression);
    this.boxForm.patchValue({
      name: box.name, description: box.description, notificationEmail: box.notificationEmail || '',
      frequency: parsed?.frequency ?? 'hourly',
      specificTime: parsed?.specificTime ?? '07:00',
      timeZoneId: box.timeZoneId,
      departmentId: box.departmentId ?? null,
      dayMon: parsed ? parsed.days.includes(1) : true,
      dayTue: parsed ? parsed.days.includes(2) : true,
      dayWed: parsed ? parsed.days.includes(3) : true,
      dayThu: parsed ? parsed.days.includes(4) : true,
      dayFri: parsed ? parsed.days.includes(5) : true,
      daySat: parsed ? parsed.days.includes(6) : false,
      daySun: parsed ? parsed.days.includes(0) : false,
      enabled: box.enabled
    });
    this.boxFormError.set(parsed ? '' : 'Existing schedule cannot be parsed; set a new one before saving.');
    this.showBoxForm.set(true);
  }

  closeBoxForm(): void { this.showBoxForm.set(false); }

  saveBox(): void {
    this.boxForm.markAllAsTouched();
    if (this.boxForm.invalid) return;
    const cronExpression = this.buildCronFromForm();
    if (!cronExpression) { this.boxFormError.set('Invalid schedule configuration.'); return; }
    this.saving.set(true);
    this.boxFormError.set('');
    const v = this.boxForm.value;
    const editing = this.editingBox();
    if (editing) {
      const req: UpdateBoxRequest = { name: v.name!, description: v.description ?? '', cronExpression, timeZoneId: v.timeZoneId!, enabled: v.enabled ?? true, notificationEmail: normalizeNotificationEmails(v.notificationEmail), departmentId: v.departmentId || undefined };
      this.boxesService.update(editing.boxId, req).subscribe({
        next: () => { this.saving.set(false); this.closeBoxForm(); this.loadBoxes(); },
        error: (err) => { this.boxFormError.set(err?.error?.message || 'Failed to save.'); this.saving.set(false); }
      });
    } else {
      const req: CreateBoxRequest = {
        name: v.name!, description: v.description ?? '', cronExpression, timeZoneId: v.timeZoneId!, notificationEmail: normalizeNotificationEmails(v.notificationEmail), departmentId: v.departmentId || undefined,
        initialTask: {
          name: v.taskName!,
          description: v.taskDescription ?? '',
          command: v.taskCommand!,
          taskType: v.taskType || 'Exe'
        }
      };
      this.boxesService.create(req).subscribe({
        next: () => { this.saving.set(false); this.closeBoxForm(); this.loadBoxes(); },
        error: (err) => { this.boxFormError.set(err?.error?.message || 'Failed to create.'); this.saving.set(false); }
      });
    }
  }

  requestDelete(box: BoxDto): void { this.deleteError.set(''); this.boxPendingDelete.set(box); }
  cancelDelete(): void { this.deleteLoading.set(false); this.deleteError.set(''); this.boxPendingDelete.set(null); }

  confirmDelete(): void {
    const box = this.boxPendingDelete();
    if (!box) return;
    this.deleteLoading.set(true);
    this.boxesService.delete(box.boxId).subscribe({
      next: () => { this.deleteLoading.set(false); this.boxPendingDelete.set(null); this.loadBoxes(); },
      error: () => { this.deleteLoading.set(false); this.deleteError.set('Failed to delete box.'); }
    });
  }

  runNow(box: BoxDto): void {
    this.runningBox.set(box);
    this.runFormGroup.reset({ ignoreDependencies: false, reason: '' });
    this.runMessage.set(''); this.runError.set('');
    this.runFormVisible.set(true);
  }

  closeRunForm(): void { this.runFormVisible.set(false); this.runMessage.set(''); this.runError.set(''); }

  confirmRun(): void {
    this.runFormGroup.markAllAsTouched();
    if (this.runFormGroup.invalid) return;
    const box = this.runningBox();
    if (!box) return;
    const v = this.runFormGroup.value;
    const req: ExecuteBoxRequest = { ignoreDependencies: v.ignoreDependencies ?? false, ignoreSchedule: false, reason: v.reason ?? '' };
    this.boxesService.runNow(box.boxId, req).subscribe({
      next: () => this.runMessage.set('Box queued successfully!'),
      error: (err) => this.runError.set(err?.error?.message || 'Failed to queue box.')
    });
  }

  // =====================================================================
  // Box detail view
  // =====================================================================
  openDetail(box: BoxDto): void {
    void this.router.navigate(['/boxes', box.boxId]);
  }

  searchTrackBy(result: SearchResultDto): string {
    return result.resultType === 'task'
      ? `task-${result.taskId ?? 0}`
      : `box-${result.boxId}`;
  }

  openTaskSearchResult(result: SearchResultDto): void {
    if (!result.taskId) return;
    void this.router.navigate(['/boxes', result.boxId, 'task', result.taskId]);
  }

  openBoxFromSearch(result: SearchResultDto): void {
    void this.router.navigate(['/boxes', result.boxId]);
  }

  closeDetail(): void { this.viewingBox.set(null); this.detailError.set(''); }

  private reloadDetail(): void {
    const box = this.viewingBox();
    if (!box) return;
    this.loadingDetail.set(true);
    this.boxesService.getById(box.boxId).subscribe({
      next: (b) => { this.viewingBox.set(b); this.loadingDetail.set(false); },
      error: () => { this.loadingDetail.set(false); }
    });
  }

  // =====================================================================
  // Task add / edit
  // =====================================================================
  openAddTask(): void {
    this.editingTask.set(null);
    this.taskForm.reset({
      name: '', description: '', command: '',
      taskType: 'Exe', dependencyTaskIds: [], enabled: true
    });
    this.taskFormError.set('');
    this.showTaskForm.set(true);
  }

  openEditTask(task: TaskDto): void {
    this.editingTask.set(task);
    this.taskForm.patchValue({
      name: task.name, description: task.description, command: task.command,
      taskType: task.taskType,
      dependencyTaskIds: task.dependencyTaskIds,
      enabled: task.enabled
    });
    this.taskFormError.set('');
    this.showTaskForm.set(true);
  }

  closeTaskForm(): void {
    this.showTaskForm.set(false);
    this.editingTask.set(null);
    this.taskForm.patchValue({ dependencyTaskIds: [] });
  }

  saveTask(): void {
    this.taskForm.markAllAsTouched();
    if (this.taskForm.invalid) return;
    const box = this.viewingBox();
    if (!box) return;
    const v = this.taskForm.value;
    const dependencyTaskIds = this.normalizeDependencyIds(v.dependencyTaskIds);
    const dependencyError = this.getDependencyValidationError(box, this.editingTask()?.taskId ?? null, dependencyTaskIds);
    if (dependencyError) { this.taskFormError.set(dependencyError); return; }

    this.taskSaving.set(true);
    this.taskFormError.set('');
    const editing = this.editingTask();
    if (editing) {
      const req: UpdateTaskRequest = {
        name: v.name!, description: v.description ?? '', command: v.command!,
        taskType: v.taskType || 'Exe', enabled: v.enabled ?? true,
        dependencyTaskIds
      };
      this.tasksService.update(editing.taskId, req).subscribe({
        next: () => { this.taskSaving.set(false); this.closeTaskForm(); this.reloadDetail(); },
        error: (err) => { this.taskFormError.set(err?.error?.message || 'Failed to save task.'); this.taskSaving.set(false); }
      });
    } else {
      const req: CreateTaskRequest = {
        boxId: box.boxId, name: v.name!, description: v.description ?? '',
        command: v.command!, taskType: v.taskType || 'Exe',
        dependencyTaskIds
      };
      this.tasksService.create(req).subscribe({
        next: () => { this.taskSaving.set(false); this.closeTaskForm(); this.reloadDetail(); },
        error: (err) => { this.taskFormError.set(err?.error?.message || 'Failed to create task.'); this.taskSaving.set(false); }
      });
    }
  }

  dependencyCandidates(): TaskDto[] {
    const box = this.viewingBox();
    if (!box) return [];
    const editingTaskId = this.editingTask()?.taskId;
    return box.tasks.filter(task => task.enabled && task.taskId !== editingTaskId);
  }

  isDependencySelected(taskId: number): boolean {
    const selected = this.normalizeDependencyIds(this.taskForm.get('dependencyTaskIds')?.value);
    return selected.includes(taskId);
  }

  toggleDependency(taskId: number, checked: boolean): void {
    const selected = this.normalizeDependencyIds(this.taskForm.get('dependencyTaskIds')?.value);
    const next = checked
      ? [...selected, taskId]
      : selected.filter(id => id !== taskId);
    this.taskForm.patchValue({ dependencyTaskIds: this.normalizeDependencyIds(next) });
  }

  dependencyLabel(task: TaskDto): string {
    const box = this.viewingBox();
    if (!box || task.dependencyTaskIds.length === 0) return 'None';
    const byId = new Map(box.tasks.map(t => [t.taskId, t.name] as const));
    return task.dependencyTaskIds.map(id => byId.get(id) ?? `Task #${id}`).join(', ');
  }

  private normalizeDependencyIds(raw: unknown): number[] {
    if (!Array.isArray(raw)) return [];
    return raw
      .map(value => Number(value))
      .filter(value => Number.isInteger(value) && value > 0)
      .filter((value, index, arr) => arr.indexOf(value) === index);
  }

  private getDependencyValidationError(box: BoxDto, taskId: number | null, dependencyTaskIds: number[]): string {
    const activeIds = new Set(box.tasks.filter(t => t.enabled).map(t => t.taskId));
    for (const depId of dependencyTaskIds) {
      if (!activeIds.has(depId)) return 'Dependencies must be active tasks in the same box.';
      if (taskId !== null && depId === taskId) return 'A task cannot depend on itself.';
    }

    if (taskId === null) return '';

    const graph = new Map<number, number[]>();
    for (const task of box.tasks) graph.set(task.taskId, [...task.dependencyTaskIds]);
    graph.set(taskId, [...dependencyTaskIds]);

    for (const depId of dependencyTaskIds) {
      if (this.hasPath(depId, taskId, graph, new Set<number>()))
        return 'Circular dependency detected.';
    }

    return '';
  }

  private hasPath(startTaskId: number, targetTaskId: number, graph: Map<number, number[]>, visited: Set<number>): boolean {
    if (startTaskId === targetTaskId) return true;
    if (visited.has(startTaskId)) return false;

    visited.add(startTaskId);
    const dependencies = graph.get(startTaskId) ?? [];
    for (const depId of dependencies) {
      if (this.hasPath(depId, targetTaskId, graph, visited)) return true;
    }
    return false;
  }

  // =====================================================================
  // Task force start
  // =====================================================================
  openForceStart(task: TaskDto): void {
    this.forceStartPendingTask.set(task);
    this.forceStartForm.reset({ reason: '' });
    this.forceStartMessage.set('');
    this.forceStartError.set('');
  }

  closeForceStart(): void {
    this.forceStartPendingTask.set(null);
    this.forceStartMessage.set('');
    this.forceStartError.set('');
  }

  confirmForceStart(): void {
    this.forceStartForm.markAllAsTouched();
    if (this.forceStartForm.invalid) return;
    const task = this.forceStartPendingTask();
    if (!task) return;
    this.forceStartLoading.set(true);
    this.forceStartError.set('');
    const req: ForceStartTaskRequest = { reason: this.forceStartForm.value.reason ?? '' };
    this.tasksService.forceStart(task.taskId, req).subscribe({
      next: () => {
        this.forceStartLoading.set(false);
        this.forceStartMessage.set(`Task '${task.name}' accepted for immediate execution.`);
      },
      error: (err) => {
        this.forceStartLoading.set(false);
        const status = err?.status;
        if (status === 409) {
          const code = err?.error?.errorCode;
          this.forceStartError.set(
            code === 'TASK_ALREADY_RUNNING'
              ? 'Task is already running.'
              : 'Task is already queued or running.'
          );
        } else {
          this.forceStartError.set(err?.error?.message || 'Failed to start task.');
        }
      }
    });
  }

  // =====================================================================
  // Task delete
  // =====================================================================
  requestDeleteTask(task: TaskDto): void { this.taskDeleteError.set(''); this.taskPendingDelete.set(task); }
  cancelDeleteTask(): void { this.taskDeleteLoading.set(false); this.taskDeleteError.set(''); this.taskPendingDelete.set(null); }

  confirmDeleteTask(): void {
    const task = this.taskPendingDelete();
    if (!task) return;
    this.taskDeleteLoading.set(true);
    this.tasksService.delete(task.taskId).subscribe({
      next: () => { this.taskDeleteLoading.set(false); this.cancelDeleteTask(); this.reloadDetail(); },
      error: () => { this.taskDeleteLoading.set(false); this.taskDeleteError.set('Failed to delete task.'); }
    });
  }

  describeCron(cron: string, timeZoneId = 'Etc/UTC'): string {
    return sharedDescribeCron(cron, timeZoneId);
  }

  liveScheduleSummary(): string {
    return this.describeCron(this.buildCronFromForm() ?? '', this.selectedTimeZoneId());
  }

  selectedTimeZoneId(): string {
    return this.boxForm.value.timeZoneId || this.userTimeZone;
  }

  formatUtc(value: string | undefined | null, variant: 'short' | 'medium' | 'date'): string {
    return formatUtcShorthand(value, this.userTimeZone, variant);
  }

  formatUtcWithBoxContext(value: string | undefined | null, boxTimeZoneId: string | undefined, variant: 'short' | 'medium'): string {
    return formatUtcWithBoxContextShorthand(value, this.userTimeZone, boxTimeZoneId, variant);
  }

  private selectedDays(): number[] {
    return this.dayOptions.filter(d => !!this.boxForm.get(d.key)?.value).map(d => d.dow);
  }

  private buildCronFromForm(): string | null {
    const days = this.selectedDays();
    if (!days.length) return null;
    const freq = this.boxForm.value.frequency as FrequencyOption;
    const dowStr = days.length === 7 ? '*' : days.join(',');
    let minutePart: string, hourPart: string;
    switch (freq) {
      case 'hourly': minutePart = '0'; hourPart = '*'; break;
      case 'every10': minutePart = '*/10'; hourPart = '*'; break;
      case 'every15': minutePart = '*/15'; hourPart = '*'; break;
      case 'every30': minutePart = '*/30'; hourPart = '*'; break;
      case 'onceDaily': {
        const [h, m] = (this.boxForm.value.specificTime || '07:00').split(':');
        minutePart = m; hourPart = h; break;
      }
      default: return null;
    }
    return minutePart + ' ' + hourPart + ' * * ' + dowStr;
  }

  private parseCronToSchedule(cron: string) {
    return parseCronToSchedule(cron);
  }

  activeBoxes(): number {
    return this.boxes().filter(box => box.enabled).length;
  }

  totalTasks(): number {
    return this.boxes().reduce((total, box) => total + box.tasks.length, 0);
  }
}
