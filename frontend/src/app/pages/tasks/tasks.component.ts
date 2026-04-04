import { Component, HostListener, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
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

@Component({
  selector: 'app-tasks',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, HighlightPipe],
  template: `
    <div class="view-shell">
      <div class="view-hero">
        <div class="view-hero-main">
          <div class="view-eyebrow">Configuration Workspace</div>
          <h1>Boxes And Tasks</h1>
          <p class="view-description">
            Manage workflow definitions, schedules, time zones, task composition and dependency structure without changing runtime behavior.
          </p>
        </div>
        <div class="view-hero-kpi">
          <span class="kpi-value">{{ loading() ? '--' : boxes().length }}</span>
          <span class="kpi-label">Boxes</span>
        </div>
        <div class="view-hero-kpi">
          <span class="kpi-value">{{ loading() ? '--' : activeBoxes() }}</span>
          <span class="kpi-label">Active</span>
        </div>
        <div class="view-hero-kpi">
          <span class="kpi-value">{{ loading() ? '--' : totalTasks() }}</span>
          <span class="kpi-label">Tasks</span>
        </div>
      </div>

      <section class="data-panel">
        <div class="panel-header">
          <div class="panel-title-wrap">
            <div class="panel-title">Global Search</div>
            <div class="panel-subtitle">Search boxes and tasks from a single entry point, then jump directly to the matching record. Use Ctrl+K or / to focus the search box.</div>
          </div>
        </div>

        <div class="panel-body task-search-shell">
          <form [formGroup]="taskSearchForm" (ngSubmit)="submitTaskSearch()" class="task-search-form" novalidate>
            <div class="field search-field">
              <label for="task-search-query">Search text</label>
              <input id="task-search-query" formControlName="query" placeholder="Try task name, command fragment, box name or time zone" [class.is-invalid]="taskSearchFieldInvalid()" />
              @if (taskSearchFieldInvalid()) { <span class="field-hint">Enter at least 2 characters.</span> }
            </div>
            <div class="field search-scope-field">
              <label for="task-search-scope">Scope</label>
              <select id="task-search-scope" formControlName="scope">
                <option value="all">All</option>
                <option value="box">Boxes</option>
                <option value="task">Tasks</option>
              </select>
            </div>
            <div class="field search-limit-field">
              <label for="task-search-limit">Limit</label>
              <select id="task-search-limit" formControlName="limit">
                <option [value]="10">10</option>
                <option [value]="25">25</option>
                <option [value]="50">50</option>
              </select>
            </div>
            <div class="search-actions">
              <button type="submit" class="btn btn-primary" [disabled]="taskSearchLoading()">{{ taskSearchLoading() ? 'Searching...' : 'Search' }}</button>
              <button type="button" class="btn" (click)="clearTaskSearch()" [disabled]="taskSearchLoading()">Clear</button>
            </div>
          </form>

          @if (taskSearchError()) {
            <div class="alert alert-danger">{{ taskSearchError() }}</div>
          }

          @if (taskSearchLoading()) {
            <div class="loading-state"><span class="spinner"></span> Searching boxes and tasks...</div>
          } @else if (!taskSearchPerformed()) {
            <div class="empty-state compact-empty"><p>Use the scope filter to search only boxes, only tasks, or both.</p></div>
          } @else if (taskSearchResults().length === 0) {
            <div class="empty-state compact-empty"><p>No results matched the current search.</p></div>
          } @else {
            <table class="data-table">
              <thead>
                <tr>
                  <th>Type</th>
                  <th>Name</th>
                  <th>Context</th>
                  <th>Details</th>
                  <th>Status</th>
                  <th>Created</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                @for (result of taskSearchResults(); track searchTrackBy(result)) {
                  <tr>
                    <td>
                      <span [class]="'type-badge ' + (result.resultType === 'task' ? 'type-api' : 'type-exe')">{{ result.resultType }}</span>
                    </td>
                    <td>
                      <strong><span [innerHTML]="result.title | highlight:taskSearchQuery()"></span></strong>
                      @if (result.description) { <div class="box-row-copy" [innerHTML]="result.description | highlight:taskSearchQuery()"></div> }
                    </td>
                    <td>
                      @if (result.resultType === 'task') {
                        <strong><span [innerHTML]="result.boxName | highlight:taskSearchQuery()"></span></strong>
                        <div class="box-row-copy">Parent box</div>
                      } @else {
                        <strong><span [innerHTML]="(result.timeZoneId || '--') | highlight:taskSearchQuery()"></span></strong>
                        <div class="box-row-copy">Time zone</div>
                      }
                    </td>
                    <td>
                      @if (result.resultType === 'task') {
                        <div class="search-detail-stack">
                          <span [class]="'type-badge type-' + result.taskType.toLowerCase()">{{ result.taskType }}</span>
                          <code class="search-command" [innerHTML]="result.command | highlight:taskSearchQuery()"></code>
                        </div>
                      } @else {
                        <div class="search-detail-stack">
                          <span>{{ result.activeTaskCount }} active task(s)</span>
                        </div>
                      }
                    </td>
                    <td>
                      <div class="search-status-stack">
                        <span [class]="'badge ' + (result.enabled ? 'badge-success' : 'badge-danger')">{{ result.enabled ? 'Enabled' : 'Disabled' }}</span>
                        @if (result.resultType === 'task') {
                          <span [class]="'badge ' + (result.boxEnabled ? 'badge-success' : 'badge-danger')">{{ result.boxEnabled ? 'Box On' : 'Box Off' }}</span>
                        }
                      </div>
                    </td>
                    <td>{{ formatUtc(result.createdAt, 'date') }}</td>
                    <td class="table-actions">
                      @if (result.resultType === 'task') {
                        <button class="btn btn-sm btn-view" (click)="openTaskSearchResult(result)">Open Task</button>
                      }
                      <button class="btn btn-sm" (click)="openBoxFromSearch(result)">Open Box</button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </section>

      <section class="data-panel">
        <div class="panel-header">
          <div class="panel-title-wrap">
            <div class="panel-title">Box Catalog</div>
            <div class="panel-subtitle">Primary registry of workflow containers, schedules and task counts.</div>
          </div>
          <div class="panel-toolbar">
            @if (auth.isAdmin) {
              <button class="btn btn-primary" (click)="openCreate()">New Box</button>
            }
          </div>
        </div>

        @if (loadError()) {
          <div class="panel-body"><div class="alert alert-danger">{{ loadError() }}</div></div>
        }

        @if (loading()) {
          <div class="loading-state"><span class="spinner"></span> Loading boxes...</div>
        } @else if (boxes().length === 0 && !loadError()) {
          <div class="empty-state"><p>No boxes configured yet.</p></div>
        } @else {
          <table class="data-table">
            <thead><tr>
              <th>Name</th><th>Department</th><th>Schedule</th><th>Time Zone</th><th>Tasks</th>
              <th>Status</th><th>Last Run</th><th>Created</th><th>Actions</th>
            </tr></thead>
            <tbody>
              @for (box of boxes(); track box.boxId) {
                <tr>
                  <td>
                    <strong>{{ box.name }}</strong>
                    @if (box.description) { <div class="box-row-copy">{{ box.description }}</div> }
                  </td>
                  <td>{{ box.departmentName || 'Not assigned' }}</td>
                  <td><span class="schedule-chip">{{ describeCron(box.cronExpression, box.timeZoneId) }}</span></td>
                  <td>{{ box.timeZoneId }}</td>
                  <td>{{ box.tasks.length }} step(s)</td>
                  <td><span [class]="'badge ' + (box.enabled ? 'badge-success' : 'badge-danger')">{{ box.enabled ? 'Active' : 'Disabled' }}</span></td>
                  <td>{{ box.lastRunUtc ? formatUtcWithBoxContext(box.lastRunUtc, box.timeZoneId, 'short') : '-' }}</td>
                  <td>{{ formatUtc(box.createdAt, 'date') }}</td>
                    <td>
                      <div class="table-actions">
                        <button class="btn btn-sm btn-view" (click)="openDetail(box)">View</button>
                        @if (auth.isOperator) {
                          <button class="btn btn-sm btn-run" (click)="runNow(box)">Run</button>
                        }
                        @if (auth.isAdmin) {
                          <button class="btn btn-sm" (click)="openEdit(box)">Edit</button>
                          <button class="btn btn-sm btn-danger" (click)="requestDelete(box)">Delete</button>
                        }
                      </div>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
      </section>
    </div>

    @if (showBoxForm()) {
      <div class="modal-overlay" role="dialog" aria-modal="true">
        <div class="modal" (click)="$event.stopPropagation()" style="max-width:720px;width:95vw;max-height:92vh;overflow-y:auto">
          <div class="modal-header">
            <h3>{{ editingBox() ? 'Edit Box' : 'New Box' }}</h3>
            <button type="button" class="modal-close" (click)="closeBoxForm()" aria-label="Close">x</button>
          </div>
          <form [formGroup]="boxForm" (ngSubmit)="saveBox()" novalidate>
            <div class="modal-body">
              <div class="field">
                <label for="b-name">Name</label>
                <input id="b-name" formControlName="name" placeholder="Nightly Reports" [class.is-invalid]="bfi('name')" />
                @if (bfi('name')) { <span class="field-hint">Name is required.</span> }
              </div>
              <div class="field">
                <label for="b-desc">Description</label>
                <input id="b-desc" formControlName="description" placeholder="Optional description" />
              </div>
              <div class="field">
                <label for="b-email">Notification Email</label>
                <input id="b-email" type="email" formControlName="notificationEmail" placeholder="Optional. Email to notify on task failure." [class.is-invalid]="bfi('notificationEmail')" />
                @if (bfi('notificationEmail')) { <span class="field-hint">Enter a valid email address.</span> }
              </div>
              <div class="field">
                <label for="b-dept">Department</label>
                <select id="b-dept" formControlName="departmentId">
                  <option [ngValue]="null">Not assigned</option>
                  @for (department of departments(); track department.departmentId) {
                    <option [ngValue]="department.departmentId">{{ department.name }}</option>
                  }
                </select>
                <span class="field-hint" style="color:var(--text-3)">Department controls governance defaults for this box.</span>
              </div>
              <section class="scheduler-box">
                <h4>Schedule</h4>
                <div class="scheduler-group">
                  <p class="scheduler-title">Days of the Week</p>
                  <div class="days-grid">
                    @for (d of dayOptions; track d.key) {
                      <label class="day-check">
                        <input type="checkbox" [formControlName]="d.key" /><span>{{ d.label }}</span>
                      </label>
                    }
                  </div>
                </div>
                <div class="scheduler-group">
                  <p class="scheduler-title">Frequency</p>
                  <div class="frequency-options">
                    <label><input type="radio" formControlName="frequency" value="hourly" /> Every Hour</label>
                    <label><input type="radio" formControlName="frequency" value="every10" /> Every 10 min</label>
                    <label><input type="radio" formControlName="frequency" value="every15" /> Every 15 min</label>
                    <label><input type="radio" formControlName="frequency" value="every30" /> Every 30 min</label>
                    <label class="inline-time-row">
                      <input type="radio" formControlName="frequency" value="onceDaily" />
                      <span>Once Daily at:</span>
                      <input type="time" formControlName="specificTime" />
                    </label>
                  </div>
                </div>
                <div class="scheduler-group">
                  <p class="scheduler-title">Time Zone</p>
                  <select formControlName="timeZoneId" [class.is-invalid]="bfi('timeZoneId')">
                    @for (tz of availableTimeZones; track tz) {
                      <option [value]="tz">{{ tz }}</option>
                    }
                  </select>
                  @if (bfi('timeZoneId')) { <span class="field-hint">Time zone is required.</span> }
                  <span class="field-hint" style="color:var(--text-3)">Detected browser time zone: {{ userTimeZone }}</span>
                </div>
                <div class="summary-box">
                  <strong>Preview:</strong>
                  <p>{{ liveScheduleSummary() }}</p>
                  <p>This schedule will run in: <strong>{{ selectedTimeZoneId() }}</strong></p>
                </div>
              </section>
              @if (editingBox()) {
                <div class="field field-check"><input type="checkbox" formControlName="enabled" id="b-en" /><label for="b-en">Enabled</label></div>
              }

              @if (!editingBox()) {
                <section class="task-section">
                  <h4>First Task <span class="required-note">(required)</span></h4>
                  <p class="task-section-hint">Every box must have at least one task. Configure the first one below.</p>
                  <div class="field">
                    <label for="t-name">Task Name</label>
                    <input id="t-name" formControlName="taskName" placeholder="e.g. Generate Report" [class.is-invalid]="bfi('taskName')" />
                    @if (bfi('taskName')) { <span class="field-hint">Task name is required.</span> }
                  </div>
                  <div class="field">
                    <label for="t-desc">Task Description</label>
                    <input id="t-desc" formControlName="taskDescription" placeholder="Optional" />
                  </div>
                  <div class="field">
                    <label for="t-cmd">Command</label>
                    <input id="t-cmd" formControlName="taskCommand" placeholder="C:\scripts\report.exe --daily" [class.is-invalid]="bfi('taskCommand')" />
                    @if (bfi('taskCommand')) { <span class="field-hint">Command is required.</span> }
                  </div>
                  <div class="field">
                    <label for="t-type">Task Type</label>
                    <select id="t-type" formControlName="taskType">
                      @for (opt of taskTypeOptions; track opt.value) {
                        <option [value]="opt.value">{{ opt.label }}</option>
                      }
                    </select>
                  </div>
                  <div class="field">
                    <label for="t-deps-init">Dependencies</label>
                    <input id="t-deps-init" value="None for first task" disabled />
                    <span class="field-hint" style="color:var(--text-3)">You can configure dependencies when adding additional tasks in the box detail view.</span>
                  </div>
                </section>
              }

              @if (boxFormError()) { <div class="alert alert-danger" style="margin-top:.75rem">{{ boxFormError() }}</div> }
            </div>
            <div class="modal-footer">
              <button type="button" class="btn" (click)="closeBoxForm()">Cancel</button>
              <button type="submit" class="btn btn-primary" [disabled]="saving()">
                {{ saving() ? 'Saving...' : (editingBox() ? 'Save Box' : 'Create Box & Task') }}
              </button>
            </div>
          </form>
        </div>
      </div>
    }

    @if (boxPendingDelete()) {
      <div class="modal-overlay" role="dialog" aria-modal="true">
        <div class="modal" (click)="$event.stopPropagation()">
          <div class="modal-header"><h3>Delete Box</h3><button type="button" class="modal-close" (click)="cancelDelete()" aria-label="Close">x</button></div>
          <div class="modal-body">
            <p>You are about to delete <strong>{{ boxPendingDelete()!.name }}</strong>.</p>
            @if (deleteError()) { <div class="alert alert-danger">{{ deleteError() }}</div> }
          </div>
          <div class="modal-footer">
            <button class="btn" (click)="cancelDelete()">Cancel</button>
            <button class="btn btn-danger" (click)="confirmDelete()" [disabled]="deleteLoading()">{{ deleteLoading() ? 'Deleting...' : 'Confirm Delete' }}</button>
          </div>
        </div>
      </div>
    }

    @if (runFormVisible()) {
      <div class="modal-overlay" role="dialog" aria-modal="true">
        <div class="modal" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h3>Run "{{ runningBox()?.name }}" now</h3>
            <button type="button" class="modal-close" (click)="closeRunForm()" aria-label="Close">x</button>
          </div>
          <form [formGroup]="runFormGroup" (ngSubmit)="confirmRun()">
            <div class="modal-body">
              <div class="field field-check"><input type="checkbox" formControlName="ignoreDependencies" id="b-idep" /><label for="b-idep">Ignore dependencies</label></div>
              <div class="field">
                <label for="b-reason">Reason <span style="color:var(--danger)">*</span></label>
                <input id="b-reason" formControlName="reason" placeholder="Reason for manual execution" [class.is-invalid]="bfr('reason')" />
                @if (bfr('reason')) { <span class="field-hint">Reason is required.</span> }
              </div>
              @if (runMessage()) { <div class="alert alert-success">{{ runMessage() }}</div> }
              @if (runError()) { <div class="alert alert-danger">{{ runError() }}</div> }
            </div>
            <div class="modal-footer">
              <button type="button" class="btn" (click)="closeRunForm()">Close</button>
              @if (!runMessage()) { <button type="submit" class="btn btn-primary">Queue Box</button> }
            </div>
          </form>
        </div>
      </div>
    }

    <!-- ==================== Box detail modal ==================== -->
    @if (viewingBox()) {
      <div class="modal-overlay" role="dialog" aria-modal="true">
        <div class="modal detail-modal" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h3>{{ viewingBox()!.name }}</h3>
            <button type="button" class="modal-close" (click)="closeDetail()" aria-label="Close">x</button>
          </div>
          <div class="modal-body">
            @if (loadingDetail()) {
              <div class="loading-state"><span class="spinner"></span> Loading details...</div>
            } @else {
              @if (viewingBox()!.description) {
                <p class="detail-description">{{ viewingBox()!.description }}</p>
              }
              <div class="detail-meta">
                <div class="detail-meta-item">
                  <span class="detail-label">Schedule</span>
                  <span class="schedule-chip">{{ describeCron(viewingBox()!.cronExpression, viewingBox()!.timeZoneId) }}</span>
                </div>
                <div class="detail-meta-item">
                  <span class="detail-label">Time Zone</span>
                  <span>{{ viewingBox()!.timeZoneId }}</span>
                </div>
                <div class="detail-meta-item">
                  <span class="detail-label">Status</span>
                  <span [class]="'badge ' + (viewingBox()!.enabled ? 'badge-success' : 'badge-danger')">{{ viewingBox()!.enabled ? 'Active' : 'Disabled' }}</span>
                </div>
                <div class="detail-meta-item">
                  <span class="detail-label">Last Run</span>
                  <span>{{ viewingBox()!.lastRunUtc ? formatUtcWithBoxContext(viewingBox()!.lastRunUtc, viewingBox()!.timeZoneId, 'short') : 'Never' }}</span>
                </div>
                <div class="detail-meta-item">
                  <span class="detail-label">Created</span>
                  <span>{{ formatUtc(viewingBox()!.createdAt, 'short') }}</span>
                </div>
              </div>
              <div class="tasks-header">
                <h4>Tasks ({{ viewingBox()!.tasks.length }})</h4>
                @if (auth.isAdmin) {
                  <button class="btn btn-sm btn-primary" (click)="openAddTask()">+ Add Task</button>
                }
              </div>
              @if (detailError()) { <div class="alert alert-danger">{{ detailError() }}</div> }
              @if (viewingBox()!.tasks.length === 0) {
                <div class="empty-state" style="padding:1.5rem 0"><p>No tasks configured for this box.</p></div>
              } @else {
                <div class="task-list">
                  @for (task of viewingBox()!.tasks; track task.taskId) {
                    <div class="task-card">
                      <div class="task-card-head">
                        <div class="task-card-title">
                          <strong>{{ task.name }}</strong>
                          <span [class]="'type-badge type-' + task.taskType.toLowerCase()">{{ task.taskType }}</span>
                          <span [class]="'badge ' + (task.enabled ? 'badge-success' : 'badge-danger')" style="font-size:.72rem">{{ task.enabled ? 'Active' : 'Off' }}</span>
                        </div>
                        @if (auth.isAdmin || auth.isOperator) {
                          <div class="task-card-actions">
                            @if (auth.isOperator) {
                              <button class="btn btn-sm" style="background:var(--info-bg);color:var(--info);border-color:transparent" (click)="openForceStart(task)">Force Start</button>
                            }
                            @if (auth.isAdmin) {
                              <button class="btn btn-sm" (click)="openEditTask(task)">Edit</button>
                              <button class="btn btn-sm btn-danger" (click)="requestDeleteTask(task)">Delete</button>
                            }
                          </div>
                        }
                      </div>
                      @if (task.description) {
                        <div class="task-card-desc">{{ task.description }}</div>
                      }
                      <div class="task-card-command">
                        <span class="cmd-label">CMD</span>
                        <code>{{ task.command }}</code>
                      </div>
                      @if (task.dependencyTaskIds.length > 0) {
                        <div class="task-card-meta">Depends on: {{ dependencyLabel(task) }}</div>
                      }
                    </div>
                  }
                </div>
              }
            }
          </div>
          <div class="modal-footer">
            <button class="btn" (click)="closeDetail()">Close</button>
          </div>
        </div>
      </div>
    }

    <!-- ==================== Task form modal ==================== -->
    @if (showTaskForm()) {
      <div class="modal-overlay modal-overlay-top" role="dialog" aria-modal="true">
        <div class="modal" (click)="$event.stopPropagation()" style="max-width:560px;width:95vw">
          <div class="modal-header">
            <h3>{{ editingTask() ? 'Edit Task' : 'Add Task' }}</h3>
            <button type="button" class="modal-close" (click)="closeTaskForm()" aria-label="Close">x</button>
          </div>
          <form [formGroup]="taskForm" (ngSubmit)="saveTask()" novalidate>
            <div class="modal-body">
              <div class="field">
                <label for="tf-name">Name</label>
                <input id="tf-name" formControlName="name" placeholder="e.g. Generate Report" [class.is-invalid]="bft('name')" />
                @if (bft('name')) { <span class="field-hint">Name is required.</span> }
              </div>
              <div class="field">
                <label for="tf-desc">Description</label>
                <input id="tf-desc" formControlName="description" placeholder="Optional" />
              </div>
              <div class="field">
                <label for="tf-cmd">Command</label>
                <input id="tf-cmd" formControlName="command" placeholder="C:\scripts\task.exe --args" [class.is-invalid]="bft('command')" />
                @if (bft('command')) { <span class="field-hint">Command is required.</span> }
              </div>
              <div class="field">
                <label for="tf-type">Task Type</label>
                <select id="tf-type" formControlName="taskType">
                  @for (opt of taskTypeOptions; track opt.value) {
                    <option [value]="opt.value">{{ opt.label }}</option>
                  }
                </select>
              </div>
              <div class="field">
                <label for="tf-deps">Dependencies</label>
                @if (dependencyCandidates().length === 0) {
                  <input id="tf-deps" value="No dependency candidates available" disabled />
                } @else {
                  <div class="dep-list" id="tf-deps">
                    @for (candidate of dependencyCandidates(); track candidate.taskId) {
                      <label class="dep-item">
                        <input
                          type="checkbox"
                          [checked]="isDependencySelected(candidate.taskId)"
                          (change)="toggleDependency(candidate.taskId, $any($event.target).checked)"
                        />
                        <span>{{ candidate.name }}</span>
                      </label>
                    }
                  </div>
                }
                <span class="field-hint" style="color:var(--text-3)">Only active tasks in this box are available.</span>
              </div>
              @if (editingTask()) {
                <div class="field field-check"><input type="checkbox" formControlName="enabled" id="tf-en" /><label for="tf-en">Enabled</label></div>
              }
              @if (taskFormError()) { <div class="alert alert-danger" style="margin-top:.75rem">{{ taskFormError() }}</div> }
            </div>
            <div class="modal-footer">
              <button type="button" class="btn" (click)="closeTaskForm()">Cancel</button>
              <button type="submit" class="btn btn-primary" [disabled]="taskSaving()">{{ taskSaving() ? 'Saving...' : 'Save Task' }}</button>
            </div>
          </form>
        </div>
      </div>
    }

    <!-- ==================== Delete task confirmation ==================== -->
    @if (taskPendingDelete()) {
      <div class="modal-overlay modal-overlay-top" role="dialog" aria-modal="true">
        <div class="modal" (click)="$event.stopPropagation()">
          <div class="modal-header"><h3>Delete Task</h3><button type="button" class="modal-close" (click)="cancelDeleteTask()" aria-label="Close">x</button></div>
          <div class="modal-body">
            <p>Delete task <strong>{{ taskPendingDelete()!.name }}</strong>?</p>
            @if (taskDeleteError()) { <div class="alert alert-danger">{{ taskDeleteError() }}</div> }
          </div>
          <div class="modal-footer">
            <button class="btn" (click)="cancelDeleteTask()">Cancel</button>
            <button class="btn btn-danger" (click)="confirmDeleteTask()" [disabled]="taskDeleteLoading()">{{ taskDeleteLoading() ? 'Deleting...' : 'Confirm Delete' }}</button>
          </div>
        </div>
      </div>
    }

    <!-- ==================== Force Start confirmation ==================== -->
    @if (forceStartPendingTask()) {
      <div class="modal-overlay" style="z-index:1200" role="dialog" aria-modal="true">
        <div class="modal" (click)="$event.stopPropagation()" style="max-width:440px;width:95vw">
          <div class="modal-header">
            <h3>Force Start Task</h3>
            <button type="button" class="modal-close" (click)="closeForceStart()" aria-label="Close">x</button>
          </div>
          <form [formGroup]="forceStartForm" (ngSubmit)="confirmForceStart()">
            <div class="modal-body">
              <p>This will execute <strong>{{ forceStartPendingTask()!.name }}</strong> ignoring its dependencies. Continue?</p>
              <div class="field" style="margin-top:.75rem">
                <label for="fs-reason">Reason <span style="color:var(--danger)">*</span></label>
                <input id="fs-reason" formControlName="reason" placeholder="e.g. Manual retry after outage" [class.is-invalid]="bffs()" />
                @if (bffs()) { <span class="field-hint">Reason is required.</span> }
              </div>
              @if (forceStartMessage()) { <div class="alert alert-success">{{ forceStartMessage() }}</div> }
              @if (forceStartError()) { <div class="alert alert-danger">{{ forceStartError() }}</div> }
            </div>
            <div class="modal-footer">
              <button type="button" class="btn" (click)="closeForceStart()">Close</button>
              @if (!forceStartMessage()) {
                <button type="submit" class="btn btn-primary" [disabled]="forceStartLoading()">{{ forceStartLoading() ? 'Starting...' : 'Force Start' }}</button>
              }
            </div>
          </form>
        </div>
      </div>
    }
  `,
  styles: [`
    .schedule-chip { display:inline-block;background:var(--bg-muted);border:1px solid var(--border);color:var(--text-2);border-radius:999px;padding:.2rem .55rem;font-size:.75rem }
    .scheduler-box { border:1px solid var(--border);border-radius:var(--radius-2);padding:1rem;background:var(--bg-surface);margin-bottom:1rem }
    .scheduler-box h4 { margin:0 0 .75rem;font-size:.95rem }
    .scheduler-group { border-top:1px solid var(--border);padding-top:.75rem;margin-top:.75rem }
    .scheduler-title { margin:0 0 .5rem;font-weight:700;color:var(--text-2);font-size:.85rem }
    .days-grid { display:grid;grid-template-columns:repeat(7,minmax(0,1fr));gap:.4rem }
    .day-check { display:inline-flex;align-items:center;gap:.4rem;font-size:.85rem;color:var(--text-2) }
    .frequency-options { display:flex;flex-direction:column;gap:.45rem }
    .frequency-options label { display:inline-flex;align-items:center;gap:.5rem;font-size:.86rem;color:var(--text-2) }
    .inline-time-row input[type='time'] { margin-left:.35rem;max-width:130px }
    .summary-box { margin-top:.85rem;background:color-mix(in srgb,var(--info-bg) 70%,white);border:1px solid color-mix(in srgb,var(--info) 20%,white);border-radius:var(--radius-2);padding:.75rem .85rem;color:var(--text-2) }
    .summary-box p { margin:.35rem 0 0;font-size:.86rem }
    .task-section { border:1px solid var(--primary,#4f6ef7);border-radius:var(--radius-2);padding:1rem 1.1rem;margin-top:1.1rem;background:color-mix(in srgb,var(--primary,#4f6ef7) 5%,white) }
    .task-section h4 { margin:0 0 .3rem;font-size:.95rem;color:var(--primary,#4f6ef7) }
    .required-note { font-size:.77rem;color:var(--text-3);font-weight:400 }
    .task-section-hint { font-size:.82rem;color:var(--text-3);margin:0 0 1rem }
    .detail-modal { max-width:700px;width:95vw;max-height:92vh;overflow-y:auto }
    .detail-description { color:var(--text-2);margin:0 0 1rem;font-size:.93rem }
    .detail-meta { display:flex;flex-wrap:wrap;gap:.65rem 1.5rem;margin-bottom:1.25rem;padding:.8rem 1rem;background:var(--bg-surface);border:1px solid var(--border);border-radius:var(--radius-2) }
    .detail-meta-item { display:flex;flex-direction:column;gap:.2rem }
    .detail-label { font-size:.7rem;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:var(--text-3) }
    .tasks-header { display:flex;align-items:center;justify-content:space-between;margin-bottom:.75rem }
    .tasks-header h4 { margin:0;font-size:.95rem }
    .task-list { display:flex;flex-direction:column;gap:.6rem }
    .task-card { border:1px solid var(--border);border-radius:var(--radius-2);padding:.75rem 1rem;background:var(--bg-surface) }
    .task-card-head { display:flex;align-items:flex-start;justify-content:space-between;gap:.5rem }
    .task-card-title { display:flex;align-items:center;gap:.5rem;flex-wrap:wrap }
    .task-sort { font-size:.72rem;color:var(--text-3);min-width:1.5rem;flex-shrink:0 }
    .task-card-actions { display:flex;gap:.35rem;flex-shrink:0 }
    .task-card-desc { font-size:.82rem;color:var(--text-3);margin:.4rem 0 .3rem }
    .task-card-command { display:flex;align-items:center;gap:.5rem;margin-top:.45rem }
    .cmd-label { font-size:.68rem;font-weight:700;text-transform:uppercase;letter-spacing:.04em;color:var(--text-3);flex-shrink:0 }
    .task-card-command code { font-size:.78rem;background:var(--bg-muted);border:1px solid var(--border);border-radius:4px;padding:.15rem .4rem;word-break:break-all }
    .task-card-meta { font-size:.78rem;color:var(--text-3);margin-top:.35rem }
    .type-badge { font-size:.68rem;font-weight:700;text-transform:uppercase;padding:.15rem .4rem;border-radius:3px;letter-spacing:.04em }
    .type-exe { background:#e8f4fd;color:#1565c0 }
    .type-bat { background:#fdf3e8;color:#c17a00 }
    .type-python { background:#e8f5e9;color:#2e7d32 }
    .type-api { background:#f3e8fd;color:#6a1b9a }
    .task-search-shell { display:flex;flex-direction:column;gap:1rem }
    .task-search-form { display:grid;grid-template-columns:minmax(0,1fr) 140px 120px auto;gap:.85rem;align-items:end }
    .search-field,.search-scope-field,.search-limit-field { margin:0 }
    .search-actions { display:flex;gap:.5rem;align-items:center;justify-content:flex-end }
    .compact-empty { padding:1rem 0 }
    :host ::ng-deep mark.search-hit { background:#fff3a3;color:#1f2937;padding:0 .12rem;border-radius:3px }
    .search-detail-stack { display:flex;flex-direction:column;gap:.4rem }
    .search-command { font-size:.78rem;background:var(--bg-muted);border:1px solid var(--border);border-radius:4px;padding:.15rem .4rem;word-break:break-all;display:block }
    .search-status-stack { display:flex;gap:.35rem;flex-wrap:wrap }
    .box-row-copy { font-size:.78rem;color:var(--text-3);margin-top:.15rem }
    .modal-overlay-top { z-index:1100 }
    .dep-list { border:1px solid var(--border);border-radius:var(--radius-1);padding:.5rem .6rem;max-height:160px;overflow:auto;background:var(--bg-surface) }
    .dep-item { display:flex;align-items:center;gap:.5rem;padding:.2rem 0;font-size:.9rem;color:var(--text-1) }
    .dep-item input[type='checkbox'] { width:1rem;height:1rem;accent-color:var(--primary);cursor:pointer }
    @media (max-width: 920px) {
      .task-search-form { grid-template-columns:1fr }
      .search-actions { justify-content:flex-start }
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
    notificationEmail: ['', Validators.email],
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

  submitTaskSearch(): void {
    this.runTaskSearch(true);
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
      const req: UpdateBoxRequest = { name: v.name!, description: v.description ?? '', cronExpression, timeZoneId: v.timeZoneId!, enabled: v.enabled ?? true, notificationEmail: v.notificationEmail || undefined, departmentId: v.departmentId || undefined };
      this.boxesService.update(editing.boxId, req).subscribe({
        next: () => { this.saving.set(false); this.closeBoxForm(); this.loadBoxes(); },
        error: (err) => { this.boxFormError.set(err?.error?.message || 'Failed to save.'); this.saving.set(false); }
      });
    } else {
      const req: CreateBoxRequest = {
        name: v.name!, description: v.description ?? '', cronExpression, timeZoneId: v.timeZoneId!, notificationEmail: v.notificationEmail || undefined, departmentId: v.departmentId || undefined,
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
