import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BoxRunTaskExecutionDto } from '../../models/models';
import { StatusBadgeComponent } from '../status-badge/status-badge.component';
import { ButtonDirective } from 'ui-design-system';

@Component({
  selector: 'app-task-table',
  standalone: true,
  imports: [CommonModule, StatusBadgeComponent, ButtonDirective],
  templateUrl: './task-table.component.html',
  styles: [`
    .task-actions { display:flex; gap:.4rem; flex-wrap:wrap; }
  `]
})
export class TaskTableComponent {
  @Input({ required: true }) tasks: BoxRunTaskExecutionDto[] = [];
  @Output() viewError = new EventEmitter<BoxRunTaskExecutionDto>();
  @Output() viewLogs = new EventEmitter<BoxRunTaskExecutionDto>();

  formatTime(value?: string): string {
    if (!value) return '--';
    return new Date(value).toLocaleString();
  }

  dependenciesLabel(task: BoxRunTaskExecutionDto): string {
    if (!task.dependsOn.length) return 'None';
    return task.dependsOn
      .map(id => this.tasks.find(t => t.taskId === id)?.name ?? `#${id}`)
      .join(', ');
  }
}
