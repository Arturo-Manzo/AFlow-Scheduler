import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TaskExecution } from '../../models/models';
import { StatusBadgeComponent } from '../status-badge/status-badge.component';

@Component({
  selector: 'app-task-table',
  standalone: true,
  imports: [CommonModule, StatusBadgeComponent],
  template: `
    <table class="data-table">
      <thead>
        <tr>
          <th>Task Name</th>
          <th>Status</th>
          <th>Start</th>
          <th>End</th>
          <th>Duration</th>
          <th>Dependencies</th>
          <th>Actions</th>
        </tr>
      </thead>
      <tbody>
        @for (task of tasks; track task.taskId) {
          <tr>
            <td><strong>{{ task.name }}</strong></td>
            <td><app-status-badge [status]="task.status" /></td>
            <td>{{ formatTime(task.startTime) }}</td>
            <td>{{ formatTime(task.endTime) }}</td>
            <td>{{ task.durationSeconds != null ? task.durationSeconds + 's' : '--' }}</td>
            <td>{{ dependenciesLabel(task) }}</td>
            <td>
              @if (task.executionId || (task.status === 'Failed' && (task.error || task.stackTrace))) {
                <div class="task-actions">
                  @if (task.executionId) {
                    <button class="btn btn-sm" (click)="viewLogs.emit(task)">View Logs</button>
                  }
                  @if (task.status === 'Failed' && (task.error || task.stackTrace)) {
                    <button class="btn btn-sm" (click)="viewError.emit(task)">View Error</button>
                  }
                </div>
              } @else {
                <span style="color:var(--text-3);font-size:.8rem">--</span>
              }
            </td>
          </tr>
        }
      </tbody>
    </table>
  `,
  styles: [`
    .task-actions { display:flex; gap:.4rem; flex-wrap:wrap; }
  `]
})
export class TaskTableComponent {
  @Input({ required: true }) tasks: TaskExecution[] = [];
  @Output() viewError = new EventEmitter<TaskExecution>();
  @Output() viewLogs = new EventEmitter<TaskExecution>();

  formatTime(value?: string): string {
    if (!value) return '--';
    return new Date(value).toLocaleString();
  }

  dependenciesLabel(task: TaskExecution): string {
    if (!task.dependsOn.length) return 'None';
    return task.dependsOn
      .map(id => this.tasks.find(t => t.taskId === id)?.name ?? `#${id}`)
      .join(', ');
  }
}
