import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BoxRunTaskExecutionDto } from '../../models/models';
import { StatusBadgeComponent } from '../status-badge/status-badge.component';
import { ButtonDirective } from 'ui-design-system';
import { TranslatePipe } from '../../shared/translate.pipe';
import { LanguageService } from '../../services/language.service';

@Component({
  selector: 'app-task-table',
  standalone: true,
  imports: [CommonModule, StatusBadgeComponent, ButtonDirective, TranslatePipe],
  templateUrl: './task-table.component.html',
  styles: [`
    .task-actions { display:flex; gap:.4rem; flex-wrap:wrap; }
  `]
})
export class TaskTableComponent {
  private readonly i18n = inject(LanguageService);
  @Input({ required: true }) tasks: BoxRunTaskExecutionDto[] = [];
  @Output() viewError = new EventEmitter<BoxRunTaskExecutionDto>();
  @Output() viewLogs = new EventEmitter<BoxRunTaskExecutionDto>();

  formatTime(value?: string): string {
    if (!value) return '--';
    return new Date(value).toLocaleString();
  }

  dependenciesLabel(task: BoxRunTaskExecutionDto): string {
    if (!task.dependsOn.length) return this.i18n.t('None');
    return task.dependsOn
      .map(id => this.tasks.find(t => t.taskId === id)?.name ?? `#${id}`)
      .join(', ');
  }
}
