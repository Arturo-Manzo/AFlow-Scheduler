import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BoxRunStatus, TaskExecutionStatus } from '../../models/models';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [CommonModule],
  template: `<span [class]="'badge status-badge ' + cssClass">{{ status }}</span>`,
  styles: [`
    .status-badge {
      font-weight: 700;
      letter-spacing: .02em;
      text-transform: uppercase;
      font-size: .7rem;
    }
    .status-pending { background: #e5e7eb; color: #374151; }
    .status-running { background: #dbeafe; color: #1d4ed8; }
    .status-stopping { background: #ede9fe; color: #6d28d9; }
    .status-success, .status-completed { background: #dcfce7; color: #166534; }
    .status-failed { background: #fee2e2; color: #991b1b; }
    .status-cancelled { background: #e5e7eb; color: #4b5563; }
    .status-partial { background: #ffedd5; color: #9a3412; }
    .status-skipped { background: #fef9c3; color: #854d0e; }
  `]
})
export class StatusBadgeComponent {
  @Input({ required: true }) status!: BoxRunStatus | TaskExecutionStatus;

  get cssClass(): string {
    return `status-${this.status.toLowerCase()}`;
  }
}
