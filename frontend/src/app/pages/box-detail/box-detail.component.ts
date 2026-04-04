import { Component, HostListener, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { forkJoin } from 'rxjs';
import {
  BoxDto,
  BoxRunDto,
  BoxRunStatus,
  CreateTaskRequest,
  DepartmentDto,
  ForceStartTaskRequest,
  TaskDto,
  UpdateBoxRequest,
  UpdateTaskRequest
} from '../../models/models';
import { StatusBadgeComponent } from '../../components/status-badge/status-badge.component';
import { AuthService } from '../../services/auth.service';
import { BoxesService } from '../../services/boxes.service';
import { DepartmentsService } from '../../services/departments.service';
import { ExecutionService } from '../../services/execution.service';
import { TasksService } from '../../services/tasks.service';
import { detectUserTimeZone, formatUtcShorthand, formatUtcWithBoxContextShorthand, getAvailableTimeZones, FrequencyOption, parseCronToSchedule, describeCron as sharedDescribeCron } from '../../shared/timezone-utils';
import { isFieldInvalid } from '../../shared/form-utils';

@Component({
  selector: 'app-box-detail',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, StatusBadgeComponent],
  template: `
    <div class="view-shell">
      <div class="page-header">
        <div>
          <a class="back-link" routerLink="/boxes">← Back to boxes</a>
          <h1>{{ box()?.name || ('Box #' + boxId()) }}</h1>
          <div class="page-subtitle">Dedicated box view with schedule outlook, recent execution state and full task composition.</div>
        </div>
        <div class="page-actions">
          <button class="btn" (click)="reload()">Refresh</button>
          @if (auth.isOperator && box()) {
            <button class="btn btn-run" (click)="openRunBox()">Run</button>
          }
          @if (auth.isAdmin && box()) {
            <button class="btn btn-secondary" (click)="openEditBox()">Edit</button>
            <button class="btn btn-danger" (click)="requestDeleteBox()">Delete</button>
          }
        </div>
      </div>

      @if (editBoxVisible()) {
        <div class="modal-overlay" role="dialog" aria-modal="true">
          <div class="modal" (click)="$event.stopPropagation()" style="max-width:720px;width:95vw;max-height:92vh;overflow-y:auto">
            <div class="modal-header">
              <h3>Edit Box</h3>
              <button type="button" class="modal-close" (click)="closeEditBox()" aria-label="Close">x</button>
            </div>
            <form [formGroup]="editBoxForm" (ngSubmit)="saveEditBox()" novalidate>
              <div class="modal-body">
                <div class="field">
                  <label for="eb-name">Name</label>
                  <input id="eb-name" formControlName="name" placeholder="Nightly Reports" [class.is-invalid]="editBoxFieldInvalid('name')" />
                  @if (editBoxFieldInvalid('name')) { <span class="field-hint">Name is required.</span> }
                </div>
                <div class="field">
                  <label for="eb-desc">Description</label>
                  <input id="eb-desc" formControlName="description" placeholder="Optional description" />
                </div>
                <div class="field">
                  <label for="eb-email">Notification Email</label>
                  <input id="eb-email" type="email" formControlName="notificationEmail" placeholder="Optional. Email to notify on task failure." [class.is-invalid]="editBoxFieldInvalid('notificationEmail')" />
                  @if (editBoxFieldInvalid('notificationEmail')) { <span class="field-hint">Enter a valid email address.</span> }
                </div>
                <div class="field">
                  <label for="eb-dept">Department</label>
                  <select id="eb-dept" formControlName="departmentId">
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
                    <select formControlName="timeZoneId" [class.is-invalid]="editBoxFieldInvalid('timeZoneId')">
                      @for (tz of availableTimeZones; track tz) {
                        <option [value]="tz">{{ tz }}</option>
                      }
                    </select>
                    @if (editBoxFieldInvalid('timeZoneId')) { <span class="field-hint">Time zone is required.</span> }
                    <span class="field-hint" style="color:var(--text-3)">Detected browser time zone: {{ userTimeZone }}</span>
                  </div>
                  <div class="summary-box">
                    <strong>Preview:</strong>
                    <p>{{ liveEditScheduleSummary() }}</p>
                    <p>This schedule will run in: <strong>{{ selectedEditTimeZoneId() }}</strong></p>
                  </div>
                </section>

                <div class="field field-check"><input type="checkbox" formControlName="enabled" id="eb-enabled" /><label for="eb-enabled">Enabled</label></div>
                @if (editBoxError()) { <div class="alert alert-danger">{{ editBoxError() }}</div> }
              </div>
              <div class="modal-footer">
                <button type="button" class="btn" (click)="closeEditBox()">Cancel</button>
                <button type="submit" class="btn btn-primary" [disabled]="editBoxSaving()">{{ editBoxSaving() ? 'Saving...' : 'Save Box' }}</button>
              </div>
            </form>
          </div>
        </div>
      }


    @if (runBoxVisible()) {
      <div class="modal-overlay" role="dialog" aria-modal="true">
        <div class="modal" (click)="$event.stopPropagation()" style="max-width:460px;width:95vw">
          <div class="modal-header">
            <h3>Run "{{ box()?.name }}" now</h3>
            <button type="button" class="modal-close" (click)="closeRunBox()" aria-label="Close">x</button>
          </div>
          <form [formGroup]="runForm" (ngSubmit)="confirmRunBox()">
            <div class="modal-body">
              <div class="field field-check"><input type="checkbox" formControlName="ignoreDependencies" id="box-run-idep" /><label for="box-run-idep">Ignore dependencies</label></div>
              <div class="field">
                <label for="box-run-reason">Reason <span style="color:var(--danger)">*</span></label>
                <input id="box-run-reason" formControlName="reason" placeholder="Reason for manual execution" [class.is-invalid]="runFieldInvalid('reason')" />
                @if (runFieldInvalid('reason')) { <span class="field-hint">Reason is required.</span> }
              </div>
              @if (runBoxMessage()) { <div class="alert alert-success">{{ runBoxMessage() }}</div> }
              @if (runBoxError()) { <div class="alert alert-danger">{{ runBoxError() }}</div> }
            </div>
            <div class="modal-footer">
              <button type="button" class="btn" (click)="closeRunBox()">Close</button>
              @if (!runBoxMessage()) {
                <button type="submit" class="btn btn-run" [disabled]="runBoxSaving()">{{ runBoxSaving() ? 'Queuing...' : 'Queue Box' }}</button>
              }
            </div>
          </form>
        </div>
      </div>
    }

    @if (deleteBoxVisible()) {
      <div class="modal-overlay" role="dialog" aria-modal="true">
        <div class="modal" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h3>Delete Box</h3>
            <button type="button" class="modal-close" (click)="cancelDeleteBox()" aria-label="Close">x</button>
          </div>
          <div class="modal-body">
            <p>You are about to delete <strong>{{ box()?.name }}</strong>.</p>
            <p class="hint-muted">This action cannot be undone.</p>
            @if (deleteBoxError()) { <div class="alert alert-danger">{{ deleteBoxError() }}</div> }
          </div>
          <div class="modal-footer">
            <button class="btn" (click)="cancelDeleteBox()">Cancel</button>
            <button class="btn btn-danger" (click)="confirmDeleteBox()" [disabled]="deleteBoxLoading()">{{ deleteBoxLoading() ? 'Deleting...' : 'Confirm Delete' }}</button>
          </div>
        </div>
      </div>
    }
      @if (loading()) {
        <div class="loading-state"><span class="spinner"></span> Loading box details...</div>
      } @else if (error()) {
        <div class="alert alert-danger">{{ error() }}</div>
      } @else if (box()) {
        <div class="view-hero compact-hero">
          <div class="view-hero-main">
            <div class="view-eyebrow">Workflow Definition</div>
            <h2>{{ box()!.name }}</h2>
            <p class="view-description">{{ box()!.description || 'No description configured for this box.' }}</p>
          </div>
          <div class="view-hero-kpi">
            <span class="kpi-value">{{ box()!.tasks.length }}</span>
            <span class="kpi-label">Tasks</span>
          </div>
          <div class="view-hero-kpi">
            <span class="kpi-value">{{ activeTaskCount() }}</span>
            <span class="kpi-label">Active</span>
          </div>
          <div class="view-hero-kpi">
            <span class="kpi-value">{{ recentRuns().length }}</span>
            <span class="kpi-label">Recent Runs</span>
          </div>
        </div>

        <div class="meta-grid">
          <div class="meta-card">
            <span class="meta-label">Status</span>
            <span class="meta-value">
              <span [class]="'badge ' + (box()!.enabled ? 'badge-success' : 'badge-danger')">{{ box()!.enabled ? 'Active' : 'Disabled' }}</span>
            </span>
          </div>
          <div class="meta-card">
            <span class="meta-label">Schedule</span>
            <span class="meta-value">{{ describeCron(box()!.cronExpression, box()!.timeZoneId) }}</span>
          </div>
          <div class="meta-card">
            <span class="meta-label">Time Zone</span>
            <span class="meta-value">{{ box()!.timeZoneId }}</span>
          </div>
          <div class="meta-card">
            <span class="meta-label">Last Run</span>
            <span class="meta-value">{{ box()!.lastRunUtc ? formatUtcWithBoxContext(box()!.lastRunUtc, box()!.timeZoneId, 'short') : 'Never' }}</span>
          </div>
          <div class="meta-card">
            <span class="meta-label">Next Run</span>
            <span class="meta-value">{{ nextRunLabel() }}</span>
          </div>
          <div class="meta-card">
            <span class="meta-label">Starts In</span>
            <span class="meta-value">{{ nextRunCountdownLabel() }}</span>
          </div>
          <div class="meta-card">
            <span class="meta-label">Last Execution Status</span>
            <span class="meta-value">
              @if (latestRun()) {
                <app-status-badge [status]="displayRunStatus(latestRun()!)" />
              } @else {
                <span>--</span>
              }
            </span>
          </div>
          <div class="meta-card">
            <span class="meta-label">Created</span>
            <span class="meta-value">{{ formatUtc(box()!.createdAt, 'short') }}</span>
          </div>
          <div class="meta-card">
            <span class="meta-label">Failure Alert Email</span>
            <span class="meta-value">{{ box()!.notificationEmail || 'Not configured' }}</span>
          </div>
          <div class="meta-card">
            <span class="meta-label">Department</span>
            <span class="meta-value">{{ box()!.departmentName || 'Not assigned' }}</span>
          </div>
        </div>

        <section class="data-panel">
          <div class="panel-header">
            <div class="panel-title-wrap">
              <div class="panel-title">Execution Outlook</div>
              <div class="panel-subtitle">Near-term schedule and health signal based on the latest completed or active runs.</div>
            </div>
          </div>
          <div class="panel-body overview-grid">
            <div class="overview-card">
              <span class="overview-label">Upcoming Window</span>
              <strong>{{ nextRunLabel() }}</strong>
              <p>{{ nextRunCountdownLabel() === '--' ? 'No upcoming run could be calculated from the current schedule.' : 'Expected according to the current cron expression and box time zone.' }}</p>
            </div>
            <div class="overview-card">
              <span class="overview-label">Latest Result</span>
              <strong>{{ latestRun() ? displayRunStatus(latestRun()!) : 'No runs yet' }}</strong>
              <p>{{ latestRun() ? latestRunSummary(latestRun()!) : 'This box does not have recent execution history yet.' }}</p>
            </div>
            <div class="overview-card">
              <span class="overview-label">Recent Health</span>
              <strong>{{ failedRecentRuns() }} failure(s) in last {{ recentRuns().length }}</strong>
              <p>{{ failedRecentRuns() === 0 ? 'Recent runs are stable.' : 'Review recent execution entries below to inspect failed or partial runs.' }}</p>
            </div>
          </div>
        </section>

        <section class="data-panel">
          <div class="panel-header">
            <div class="panel-title-wrap">
              <div class="panel-title">Recent Executions</div>
              <div class="panel-subtitle">Latest BoxRuns for this box with status, trigger and timing context.</div>
            </div>
          </div>

          @if (recentRuns().length === 0) {
            <div class="empty-state"><p>No recent executions found for this box.</p></div>
          } @else {
            <table class="data-table">
              <thead>
                <tr>
                  <th>Run</th>
                  <th>Status</th>
                  <th>Trigger</th>
                  <th>Scheduled</th>
                  <th>Started</th>
                  <th>Duration</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                @for (run of recentRuns(); track run.id) {
                  <tr>
                    <td>#{{ run.id }}</td>
                    <td><app-status-badge [status]="displayRunStatus(run)" /></td>
                    <td>{{ run.triggerSource }}</td>
                    <td>{{ formatUtcWithBoxContext(run.scheduledForUtc, box()!.timeZoneId, 'short') }}</td>
                    <td>{{ formatUtcWithBoxContext(run.startTime, box()!.timeZoneId, 'short') }}</td>
                    <td>{{ formatDurationSeconds(run.durationSeconds) }}</td>
                      <td>
                        <div class="table-actions">
                          <a class="btn btn-sm btn-view" [routerLink]="['/executions', run.id]">View Run</a>
                        </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </section>

        <section class="data-panel">
          <div class="panel-header">
            <div class="panel-title-wrap">
              <div class="panel-title">Tasks</div>
              <div class="panel-subtitle">Execution units configured in this box, including dependencies and task-level actions.</div>
            </div>
            <div class="panel-toolbar">
              @if (auth.isAdmin) {
                <button class="btn btn-primary" (click)="openAddTask()">New Task</button>
              }
            </div>
          </div>

          @if (box()!.tasks.length === 0) {
            <div class="empty-state"><p>No tasks configured for this box.</p></div>
          } @else {
            <div class="task-list panel-body">
              @for (task of box()!.tasks; track task.taskId) {
                <div class="task-card">
                  <div class="task-card-head">
                    <div class="task-card-title">
                      <strong>{{ task.name }}</strong>
                      <span [class]="'type-badge type-' + task.taskType.toLowerCase()">{{ task.taskType }}</span>
                      <span [class]="'badge ' + (task.enabled ? 'badge-success' : 'badge-danger')">{{ task.enabled ? 'Active' : 'Off' }}</span>
                    </div>
                    <div class="task-card-actions">
                      <a class="btn btn-sm btn-view" [routerLink]="['/boxes', boxId(), 'task', task.taskId]">View</a>
                      @if (auth.isOperator) {
                        <button class="btn btn-sm btn-run" (click)="openForceStart(task)">Force Start</button>
                      }
                      @if (auth.isAdmin) {
                        <button class="btn btn-sm" (click)="openEditTask(task)">Edit</button>
                        <button class="btn btn-sm btn-danger" (click)="requestDeleteTask(task)">Delete</button>
                      }
                    </div>
                  </div>
                  @if (task.description) {
                    <div class="task-card-desc">{{ task.description }}</div>
                  }
                  <div class="task-card-grid">
                    <div>
                      <span class="detail-label">Command</span>
                      <code>{{ task.command }}</code>
                    </div>
                    <div>
                      <span class="detail-label">Created</span>
                      <div>{{ formatUtc(task.createdAt, 'short') }}</div>
                    </div>
                    <div>
                      <span class="detail-label">Dependencies</span>
                      <div>{{ dependencyLabel(task) }}</div>
                    </div>
                  </div>
                </div>
              }
            </div>
          }
        </section>
      }
    </div>

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
                <input id="tf-name" formControlName="name" placeholder="e.g. Generate Report" [class.is-invalid]="fieldInvalid('name')" />
                @if (fieldInvalid('name')) { <span class="field-hint">Name is required.</span> }
              </div>
              <div class="field">
                <label for="tf-desc">Description</label>
                <input id="tf-desc" formControlName="description" placeholder="Optional" />
              </div>
              <div class="field">
                <label for="tf-cmd">Command</label>
                <input id="tf-cmd" formControlName="command" placeholder="C:\scripts\task.exe --args" [class.is-invalid]="fieldInvalid('command')" />
                @if (fieldInvalid('command')) { <span class="field-hint">Command is required.</span> }
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
                <span class="field-hint hint-muted">Only active tasks in this box are available.</span>
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
                <input id="fs-reason" formControlName="reason" placeholder="e.g. Manual retry after outage" [class.is-invalid]="forceStartFieldInvalid()" />
                @if (forceStartFieldInvalid()) { <span class="field-hint">Reason is required.</span> }
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
    .back-link { color: var(--text-2); text-decoration: none; font-size: .85rem; }
    .back-link:hover { text-decoration: underline; }
    .page-subtitle { margin-top: .35rem; }
    .compact-hero { margin-bottom: 1rem; }
    .overview-grid { display:grid; grid-template-columns:repeat(3, minmax(0, 1fr)); gap:.85rem; }
    .overview-card { border:1px solid var(--border); background:var(--bg-muted); border-radius:var(--radius-2); padding:1rem; }
    .overview-label { display:block; font-size:.72rem; font-weight:700; letter-spacing:.06em; text-transform:uppercase; color:var(--text-3); margin-bottom:.45rem; }
    .overview-card strong { display:block; font-size:1rem; color:var(--text-1); margin-bottom:.35rem; }
    .overview-card p { margin:0; font-size:.84rem; color:var(--text-3); }
    .task-list { display:flex; flex-direction:column; gap:.75rem; }
    .task-card { border:1px solid var(--border); border-radius:var(--radius-2); padding:1rem; background:var(--bg-surface); }
    .task-card-head { display:flex; align-items:flex-start; justify-content:space-between; gap:.75rem; }
    .task-card-title { display:flex; align-items:center; gap:.5rem; flex-wrap:wrap; }
    .task-card-actions { display:flex; gap:.35rem; flex-wrap:wrap; justify-content:flex-end; }
    .task-card-desc { font-size:.84rem; color:var(--text-3); margin:.5rem 0 .75rem; }
    .task-card-grid { display:grid; grid-template-columns:2fr 1fr 1.2fr; gap:.85rem; }
    .task-card-grid code { font-size:.78rem; background:var(--bg-muted); border:1px solid var(--border); border-radius:4px; padding:.18rem .42rem; word-break:break-all; display:block; }
    .type-badge { font-size:.68rem; font-weight:700; text-transform:uppercase; padding:.15rem .4rem; border-radius:3px; letter-spacing:.04em; }
    .type-exe { background:#e8f4fd; color:#1565c0; }
    .type-bat { background:#fdf3e8; color:#c17a00; }
    .type-python { background:#e8f5e9; color:#2e7d32; }
    .type-api { background:#f3e8fd; color:#6a1b9a; }
    .dep-list { border:1px solid var(--border); border-radius:var(--radius-1); padding:.5rem .6rem; max-height:160px; overflow:auto; background:var(--bg-surface); }
    .dep-item { display:flex; align-items:center; gap:.5rem; padding:.2rem 0; font-size:.9rem; color:var(--text-1); }
    .dep-item input[type='checkbox'] { width:1rem; height:1rem; accent-color:var(--primary); cursor:pointer; }
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
    .hint-muted { color:var(--text-3); }
    @media (max-width: 1100px) {
      .overview-grid { grid-template-columns:1fr; }
      .task-card-grid { grid-template-columns:1fr; }
    }
    @media (max-width: 720px) {
      .task-card-head { flex-direction:column; }
      .task-card-actions { justify-content:flex-start; }
    }
  `]
})
export class BoxDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private boxesService = inject(BoxesService);
  private departmentsService = inject(DepartmentsService);
  private tasksService = inject(TasksService);
  private executionService = inject(ExecutionService);
  readonly auth = inject(AuthService);
  private fb = inject(FormBuilder);
  readonly userTimeZone = Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
  readonly availableTimeZones = getAvailableTimeZones(this.userTimeZone);
  readonly dayOptions = [
    { key: 'dayMon', label: 'Mon', dow: 1 },
    { key: 'dayTue', label: 'Tue', dow: 2 },
    { key: 'dayWed', label: 'Wed', dow: 3 },
    { key: 'dayThu', label: 'Thu', dow: 4 },
    { key: 'dayFri', label: 'Fri', dow: 5 },
    { key: 'daySat', label: 'Sat', dow: 6 },
    { key: 'daySun', label: 'Sun', dow: 0 }
  ] as const;

  boxId = signal(0);
  box = signal<BoxDto | null>(null);
  departments = signal<DepartmentDto[]>([]);
  recentRuns = signal<BoxRunDto[]>([]);
  loading = signal(true);
  error = signal('');

  showTaskForm = signal(false);
  editingTask = signal<TaskDto | null>(null);
  taskFormError = signal('');
  taskSaving = signal(false);
  taskPendingDelete = signal<TaskDto | null>(null);
  taskDeleteLoading = signal(false);
  taskDeleteError = signal('');

  editBoxVisible = signal(false);
  editBoxSaving = signal(false);
  editBoxError = signal('');

  forceStartPendingTask = signal<TaskDto | null>(null);
  forceStartLoading = signal(false);
  forceStartMessage = signal('');
  forceStartError = signal('');

  runBoxVisible = signal(false);
  runBoxSaving = signal(false);
  runBoxMessage = signal('');
  runBoxError = signal('');

  deleteBoxVisible = signal(false);
  deleteBoxLoading = signal(false);
  deleteBoxError = signal('');

  private nextRunCacheKey = '';
  private nextRunCacheValue: Date | null = null;
  private readonly formatterByTimeZone = new Map<string, Intl.DateTimeFormat>();

  readonly taskTypeOptions = [
    { value: 'Exe', label: 'Exe (.exe process)' },
    { value: 'Bat', label: 'Bat (batch script)' },
    { value: 'Python', label: 'Python script' },
    { value: 'Api', label: 'Api (HTTP request)' }
  ];

  taskForm = this.fb.group({
    name: ['', Validators.required],
    description: [''],
    command: ['', Validators.required],
    taskType: ['Exe', Validators.required],
    dependencyTaskIds: [[] as number[]],
    enabled: [true]
  });

  forceStartForm = this.fb.group({
    reason: ['', Validators.required]
  });

  runForm = this.fb.group({
    ignoreDependencies: [false],
    reason: ['', Validators.required]
  });

  editBoxForm = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    description: [''],
    notificationEmail: ['', Validators.email],
    frequency: ['hourly' as FrequencyOption],
    specificTime: ['07:00'],
    timeZoneId: [this.userTimeZone, Validators.required],
    dayMon: [true], dayTue: [true], dayWed: [true], dayThu: [true],
    dayFri: [true], daySat: [false], daySun: [false],
    enabled: [true],
    departmentId: [this.auth.currentUser()?.departmentId ?? null]
  });

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.editBoxVisible()) { this.closeEditBox(); return; }
    if (this.runBoxVisible()) { this.closeRunBox(); return; }
    if (this.deleteBoxVisible()) { this.cancelDeleteBox(); return; }
    if (this.forceStartPendingTask()) { this.closeForceStart(); return; }
    if (this.showTaskForm()) { this.closeTaskForm(); return; }
    if (this.taskPendingDelete()) { this.cancelDeleteTask(); }
  }

  ngOnInit(): void {
    this.boxId.set(Number(this.route.snapshot.paramMap.get('boxId')) || 0);
    this.loadDepartments();
    this.reload();
  }

  private loadDepartments(): void {
    this.departmentsService.getAll().subscribe({
      next: (depts) => this.departments.set(depts),
      error: () => this.departments.set([])
    });
  }

  reload(): void {
    const id = this.boxId();
    if (!id) {
      this.error.set('Invalid box id.');
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.error.set('');

    forkJoin({
      box: this.boxesService.getById(id),
      runs: this.executionService.getBoxRuns(6, id)
    }).subscribe({
      next: ({ box, runs }) => {
        this.resetNextRunCache();
        this.box.set(box);
        this.recentRuns.set(runs);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load box details.');
        this.loading.set(false);
      }
    });
  }

  fieldInvalid(field: string): boolean {
    const control = this.taskForm.get(field)!;
    return control.invalid && (control.dirty || control.touched);
  }

  forceStartFieldInvalid(): boolean {
    const control = this.forceStartForm.get('reason')!;
    return control.invalid && (control.dirty || control.touched);
  }

  runFieldInvalid(field: string): boolean {
    const control = this.runForm.get(field)!;
    return control.invalid && (control.dirty || control.touched);
  }

  openRunBox(): void {
    this.runForm.reset({ ignoreDependencies: false, reason: '' });
    this.runBoxMessage.set('');
    this.runBoxError.set('');
    this.runBoxVisible.set(true);
  }

  closeRunBox(): void {
    this.runBoxVisible.set(false);
    this.runBoxError.set('');
    this.runBoxMessage.set('');
    this.runBoxSaving.set(false);
  }

  confirmRunBox(): void {
    this.runForm.markAllAsTouched();
    if (this.runForm.invalid || !this.box()) return;

    this.runBoxSaving.set(true);
    this.runBoxError.set('');

    const value = this.runForm.value;
    this.boxesService.runNow(this.box()!.boxId, {
      ignoreDependencies: value.ignoreDependencies ?? false,
      ignoreSchedule: false,
      reason: value.reason ?? ''
    }).subscribe({
      next: () => {
        this.runBoxSaving.set(false);
        this.runBoxMessage.set(`Box '${this.box()!.name}' queued for execution.`);
      },
      error: (err) => {
        this.runBoxSaving.set(false);
        this.runBoxError.set(err?.error?.message || 'Failed to queue box run.');
      }
    });
  }

  openEditBox(): void {
    const currentBox = this.box();
    if (!currentBox) return;

    const parsed = this.parseCronToSchedule(currentBox.cronExpression);

    this.editBoxForm.reset({
      name: currentBox.name,
      description: currentBox.description ?? '',
      notificationEmail: currentBox.notificationEmail ?? '',
      frequency: parsed?.frequency ?? 'hourly',
      specificTime: parsed?.specificTime ?? '07:00',
      timeZoneId: currentBox.timeZoneId,
      dayMon: parsed ? parsed.days.includes(1) : true,
      dayTue: parsed ? parsed.days.includes(2) : true,
      dayWed: parsed ? parsed.days.includes(3) : true,
      dayThu: parsed ? parsed.days.includes(4) : true,
      dayFri: parsed ? parsed.days.includes(5) : true,
      daySat: parsed ? parsed.days.includes(6) : false,
      daySun: parsed ? parsed.days.includes(0) : false,
      enabled: currentBox.enabled,
      departmentId: currentBox.departmentId ?? null
    });
    this.editBoxError.set(parsed ? '' : 'Existing schedule cannot be parsed; set a new one before saving.');
    this.editBoxVisible.set(true);
  }

  closeEditBox(): void {
    this.editBoxVisible.set(false);
    this.editBoxSaving.set(false);
    this.editBoxError.set('');
  }

  editBoxFieldInvalid(field: string): boolean {
    const control = this.editBoxForm.get(field);
    return !!control && control.invalid && (control.dirty || control.touched);
  }

  saveEditBox(): void {
    this.editBoxForm.markAllAsTouched();
    const currentBox = this.box();
    if (this.editBoxForm.invalid || !currentBox) return;

    const cronExpression = this.buildCronFromEditForm();
    if (!cronExpression) {
      this.editBoxError.set('Invalid schedule configuration.');
      return;
    }

    const value = this.editBoxForm.value;
    const request: UpdateBoxRequest = {
      name: value.name ?? currentBox.name,
      description: value.description ?? '',
      cronExpression,
      timeZoneId: value.timeZoneId ?? currentBox.timeZoneId,
      enabled: value.enabled ?? currentBox.enabled,
      notificationEmail: value.notificationEmail?.trim() || undefined,
      departmentId: value.departmentId || undefined
    };

    this.editBoxSaving.set(true);
    this.editBoxError.set('');
    this.boxesService.update(currentBox.boxId, request).subscribe({
      next: (updated) => {
        this.box.set({
          ...currentBox,
          ...updated,
          tasks: currentBox.tasks
        });
        this.resetNextRunCache();
        this.editBoxSaving.set(false);
        this.editBoxVisible.set(false);
      },
      error: (err) => {
        this.editBoxSaving.set(false);
        this.editBoxError.set(err?.error?.message || 'Failed to update box.');
      }
    });
  }

  requestDeleteBox(): void {
    this.deleteBoxError.set('');
    this.deleteBoxVisible.set(true);
  }

  cancelDeleteBox(): void {
    this.deleteBoxLoading.set(false);
    this.deleteBoxError.set('');
    this.deleteBoxVisible.set(false);
  }

  confirmDeleteBox(): void {
    const currentBox = this.box();
    if (!currentBox) return;

    this.deleteBoxLoading.set(true);
    this.boxesService.delete(currentBox.boxId).subscribe({
      next: () => {
        this.deleteBoxLoading.set(false);
        this.deleteBoxVisible.set(false);
        this.router.navigate(['/boxes']);
      },
      error: (err) => {
        this.deleteBoxLoading.set(false);
        this.deleteBoxError.set(err?.error?.message || 'Failed to delete box.');
      }
    });
  }

  openAddTask(): void {
    this.editingTask.set(null);
    this.taskForm.reset({
      name: '',
      description: '',
      command: '',
      taskType: 'Exe',
      dependencyTaskIds: [],
      enabled: true
    });
    this.taskFormError.set('');
    this.showTaskForm.set(true);
  }

  openEditTask(task: TaskDto): void {
    this.editingTask.set(task);
    this.taskForm.patchValue({
      name: task.name,
      description: task.description,
      command: task.command,
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
    if (this.taskForm.invalid || !this.box()) return;

    const currentBox = this.box()!;
    const value = this.taskForm.value;
    const dependencyTaskIds = this.normalizeDependencyIds(value.dependencyTaskIds);
    const dependencyError = this.getDependencyValidationError(currentBox, this.editingTask()?.taskId ?? null, dependencyTaskIds);
    if (dependencyError) {
      this.taskFormError.set(dependencyError);
      return;
    }

    this.taskSaving.set(true);
    this.taskFormError.set('');

    const editing = this.editingTask();
    if (editing) {
      const request: UpdateTaskRequest = {
        name: value.name!,
        description: value.description ?? '',
        command: value.command!,
        taskType: value.taskType || 'Exe',
        enabled: value.enabled ?? true,
        dependencyTaskIds
      };

      this.tasksService.update(editing.taskId, request).subscribe({
        next: () => {
          this.taskSaving.set(false);
          this.closeTaskForm();
          this.reload();
        },
        error: (err) => {
          this.taskFormError.set(err?.error?.message || 'Failed to save task.');
          this.taskSaving.set(false);
        }
      });
      return;
    }

    const request: CreateTaskRequest = {
      boxId: currentBox.boxId,
      name: value.name!,
      description: value.description ?? '',
      command: value.command!,
      taskType: value.taskType || 'Exe',
      dependencyTaskIds
    };

    this.tasksService.create(request).subscribe({
      next: () => {
        this.taskSaving.set(false);
        this.closeTaskForm();
        this.reload();
      },
      error: (err) => {
        this.taskFormError.set(err?.error?.message || 'Failed to create task.');
        this.taskSaving.set(false);
      }
    });
  }

  dependencyCandidates(): TaskDto[] {
    const currentBox = this.box();
    if (!currentBox) return [];
    const editingTaskId = this.editingTask()?.taskId;
    return currentBox.tasks.filter(task => task.enabled && task.taskId !== editingTaskId);
  }

  isDependencySelected(taskId: number): boolean {
    const selected = this.normalizeDependencyIds(this.taskForm.get('dependencyTaskIds')?.value);
    return selected.includes(taskId);
  }

  toggleDependency(taskId: number, checked: boolean): void {
    const selected = this.normalizeDependencyIds(this.taskForm.get('dependencyTaskIds')?.value);
    const next = checked ? [...selected, taskId] : selected.filter(id => id !== taskId);
    this.taskForm.patchValue({ dependencyTaskIds: this.normalizeDependencyIds(next) });
  }

  dependencyLabel(task: TaskDto): string {
    const currentBox = this.box();
    if (!currentBox || task.dependencyTaskIds.length === 0) return 'None';
    const byId = new Map(currentBox.tasks.map(item => [item.taskId, item.name] as const));
    return task.dependencyTaskIds.map(id => byId.get(id) ?? `Task #${id}`).join(', ');
  }

  requestDeleteTask(task: TaskDto): void {
    this.taskDeleteError.set('');
    this.taskPendingDelete.set(task);
  }

  cancelDeleteTask(): void {
    this.taskDeleteLoading.set(false);
    this.taskDeleteError.set('');
    this.taskPendingDelete.set(null);
  }

  confirmDeleteTask(): void {
    const task = this.taskPendingDelete();
    if (!task) return;

    this.taskDeleteLoading.set(true);
    this.tasksService.delete(task.taskId).subscribe({
      next: () => {
        this.taskDeleteLoading.set(false);
        this.cancelDeleteTask();
        this.reload();
      },
      error: () => {
        this.taskDeleteLoading.set(false);
        this.taskDeleteError.set('Failed to delete task.');
      }
    });
  }

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
    if (this.forceStartForm.invalid || !this.forceStartPendingTask()) return;

    const task = this.forceStartPendingTask()!;
    this.forceStartLoading.set(true);
    this.forceStartError.set('');

    const request: ForceStartTaskRequest = {
      reason: this.forceStartForm.value.reason ?? ''
    };

    this.tasksService.forceStart(task.taskId, request).subscribe({
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

  activeTaskCount(): number {
    return this.box()?.tasks.filter(task => task.enabled).length ?? 0;
  }

  latestRun(): BoxRunDto | null {
    return this.recentRuns()[0] ?? null;
  }

  failedRecentRuns(): number {
    return this.recentRuns().filter(run => {
      const status = this.displayRunStatus(run);
      return status === 'Failed' || status === 'Partial' || status === 'Cancelled';
    }).length;
  }

  displayRunStatus(run: BoxRunDto): BoxRunStatus {
    return run.isCancellationRequested && run.status === 'Running' ? 'Stopping' : run.status;
  }

  latestRunSummary(run: BoxRunDto): string {
    const startText = this.formatUtcWithBoxContext(run.startTime, this.box()?.timeZoneId, 'short');
    return `${run.triggerSource} trigger, started ${startText}, duration ${this.formatDurationSeconds(run.durationSeconds)}.`;
  }

  nextRunLabel(): string {
    const currentBox = this.box();
    if (!currentBox) return '--';
    if (!currentBox.enabled) return 'Disabled';

    const nextRun = this.getCachedNextRun(currentBox);
    return nextRun
      ? this.formatUtcWithBoxContext(nextRun.toISOString(), currentBox.timeZoneId, 'short')
      : '--';
  }

  nextRunCountdownLabel(): string {
    const currentBox = this.box();
    if (!currentBox || !currentBox.enabled) return '--';

    const nextRun = this.getCachedNextRun(currentBox);
    if (!nextRun) return '--';

    const diffMs = nextRun.getTime() - Date.now();
    if (diffMs <= 0) return 'less than 1 minute';

    const totalMinutes = Math.round(diffMs / 60000);
    const days = Math.floor(totalMinutes / 1440);
    const hours = Math.floor((totalMinutes % 1440) / 60);
    const minutes = totalMinutes % 60;

    if (days > 0) return `${days}d ${hours}h ${minutes}m`;
    if (hours > 0) return `${hours}h ${minutes}m`;
    return `${minutes}m`;
  }

  describeCron(cron: string, timeZoneId = 'Etc/UTC'): string {
    return sharedDescribeCron(cron, timeZoneId);
  }

  liveEditScheduleSummary(): string {
    return this.describeCron(this.buildCronFromEditForm() ?? '', this.selectedEditTimeZoneId());
  }

  selectedEditTimeZoneId(): string {
    return this.editBoxForm.value.timeZoneId || this.userTimeZone;
  }

  formatUtc(value: string | undefined | null, variant: 'short' | 'medium' | 'date'): string {
    return formatUtcShorthand(value, this.userTimeZone, variant);
  }

  formatUtcWithBoxContext(value: string | undefined | null, boxTimeZoneId: string | undefined, variant: 'short' | 'medium'): string {
    return formatUtcWithBoxContextShorthand(value, this.userTimeZone, boxTimeZoneId, variant);
  }

  formatDurationSeconds(value?: number | null): string {
    if (value == null) return '--';
    if (value < 60) return `${value}s`;

    const hours = Math.floor(value / 3600);
    const minutes = Math.floor((value % 3600) / 60);
    const seconds = value % 60;

    if (hours > 0) return `${hours}h ${minutes}m ${seconds}s`;
    return `${minutes}m ${seconds}s`;
  }

  private normalizeDependencyIds(raw: unknown): number[] {
    if (!Array.isArray(raw)) return [];
    return raw
      .map(value => Number(value))
      .filter(value => Number.isInteger(value) && value > 0)
      .filter((value, index, values) => values.indexOf(value) === index);
  }

  private getDependencyValidationError(box: BoxDto, taskId: number | null, dependencyTaskIds: number[]): string {
    const activeIds = new Set(box.tasks.filter(task => task.enabled).map(task => task.taskId));
    for (const dependencyId of dependencyTaskIds) {
      if (!activeIds.has(dependencyId)) return 'Dependencies must be active tasks in the same box.';
      if (taskId !== null && dependencyId === taskId) return 'A task cannot depend on itself.';
    }

    if (taskId === null) return '';

    const graph = new Map<number, number[]>();
    for (const task of box.tasks) graph.set(task.taskId, [...task.dependencyTaskIds]);
    graph.set(taskId, [...dependencyTaskIds]);

    for (const dependencyId of dependencyTaskIds) {
      if (this.hasPath(dependencyId, taskId, graph, new Set<number>())) {
        return 'Circular dependency detected.';
      }
    }

    return '';
  }

  private hasPath(startTaskId: number, targetTaskId: number, graph: Map<number, number[]>, visited: Set<number>): boolean {
    if (startTaskId === targetTaskId) return true;
    if (visited.has(startTaskId)) return false;

    visited.add(startTaskId);
    const dependencies = graph.get(startTaskId) ?? [];
    for (const dependencyId of dependencies) {
      if (this.hasPath(dependencyId, targetTaskId, graph, visited)) return true;
    }

    return false;
  }

  private parseCronToSchedule(cron: string) {
    return parseCronToSchedule(cron);
  }

  private selectedEditDays(): number[] {
    return this.dayOptions
      .filter(d => !!this.editBoxForm.get(d.key)?.value)
      .map(d => d.dow);
  }

  private buildCronFromEditForm(): string | null {
    const days = this.selectedEditDays();
    if (!days.length) return null;

    const freq = this.editBoxForm.value.frequency as FrequencyOption;
    const dowStr = days.length === 7 ? '*' : days.join(',');
    let minutePart: string;
    let hourPart: string;

    switch (freq) {
      case 'hourly': minutePart = '0'; hourPart = '*'; break;
      case 'every10': minutePart = '*/10'; hourPart = '*'; break;
      case 'every15': minutePart = '*/15'; hourPart = '*'; break;
      case 'every30': minutePart = '*/30'; hourPart = '*'; break;
      case 'onceDaily': {
        const [h, m] = (this.editBoxForm.value.specificTime || '07:00').split(':');
        minutePart = m;
        hourPart = h;
        break;
      }
      default: return null;
    }

    return minutePart + ' ' + hourPart + ' * * ' + dowStr;
  }

  private getCachedNextRun(currentBox: BoxDto): Date | null {
    const cacheKey = `${currentBox.cronExpression}|${currentBox.timeZoneId}`;
    if (this.nextRunCacheKey !== cacheKey) {
      this.nextRunCacheKey = cacheKey;
      this.nextRunCacheValue = this.getNextRunUtc(currentBox.cronExpression, currentBox.timeZoneId);
    }

    return this.nextRunCacheValue;
  }

  private resetNextRunCache(): void {
    this.nextRunCacheKey = '';
    this.nextRunCacheValue = null;
  }

  private getNextRunUtc(cron: string, timeZoneId: string): Date | null {
    const parts = this.parseCronParts(cron);
    if (!parts) return null;

    const now = new Date();
    let probe = new Date(Math.ceil(now.getTime() / 60000) * 60000);

    for (let index = 0; index < 14 * 24 * 60; index += 1) {
      if (this.matchesCron(parts, probe, timeZoneId)) return probe;
      probe = new Date(probe.getTime() + 60000);
    }

    return null;
  }

  private parseCronParts(cron: string): { minute: string; hour: string; days: Set<number> | null } | null {
    const parts = cron.trim().split(/\s+/);
    if (parts.length !== 5) return null;

    const [minute, hour, , , dayOfWeek] = parts;
    const days = dayOfWeek === '*'
      ? null
      : new Set(dayOfWeek.split(',').map(Number).filter(day => Number.isInteger(day) && day >= 0 && day <= 6));

    return { minute, hour, days };
  }

  private matchesCron(parts: { minute: string; hour: string; days: Set<number> | null }, value: Date, timeZoneId: string): boolean {
    const zoned = this.getZonedDateParts(value, timeZoneId);
    if (!zoned) return false;

    if (parts.days && !parts.days.has(zoned.dayOfWeek)) return false;
    if (!this.matchesCronField(parts.hour, zoned.hour)) return false;
    if (!this.matchesCronField(parts.minute, zoned.minute)) return false;
    return true;
  }

  private matchesCronField(field: string, current: number): boolean {
    if (field === '*') return true;
    if (field.startsWith('*/')) {
      const step = Number(field.slice(2));
      return Number.isInteger(step) && step > 0 && current % step === 0;
    }

    return Number(field) === current;
  }

  private getZonedDateParts(value: Date, timeZoneId: string): { hour: number; minute: number; dayOfWeek: number } | null {
    try {
      let formatter = this.formatterByTimeZone.get(timeZoneId);
      if (!formatter) {
        formatter = new Intl.DateTimeFormat('en-US', {
          timeZone: timeZoneId,
          hour12: false,
          weekday: 'short',
          hour: '2-digit',
          minute: '2-digit'
        });
        this.formatterByTimeZone.set(timeZoneId, formatter);
      }

      const parts = formatter.formatToParts(value);
      const weekday = parts.find(part => part.type === 'weekday')?.value;
      const hour = Number(parts.find(part => part.type === 'hour')?.value);
      const minute = Number(parts.find(part => part.type === 'minute')?.value);
      const dayOfWeek = this.weekdayToNumber(weekday);

      if (Number.isNaN(hour) || Number.isNaN(minute) || dayOfWeek === null) return null;
      return { hour, minute, dayOfWeek };
    } catch {
      return null;
    }
  }

  private weekdayToNumber(value?: string): number | null {
    switch (value) {
      case 'Sun': return 0;
      case 'Mon': return 1;
      case 'Tue': return 2;
      case 'Wed': return 3;
      case 'Thu': return 4;
      case 'Fri': return 5;
      case 'Sat': return 6;
      default: return null;
    }
  }
}