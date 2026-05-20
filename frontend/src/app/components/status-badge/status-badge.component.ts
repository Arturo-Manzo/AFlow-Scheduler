import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BoxRunStatus, TaskExecutionStatus } from '../../models/models';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './status-badge.component.html',
  styles: [`
    .status-badge {
      font-weight: 700;
      letter-spacing: .02em;
      text-transform: uppercase;
      font-size: .7rem;
    }
    .status-pending { background: #e5e7eb; color: #374151; border-color: #d1d5db; }
    .status-running { background: #dbeafe; color: #1d4ed8; border-color: #bfdbfe; }
    .status-stopping { background: #ede9fe; color: #6d28d9; border-color: #ddd6fe; }
    .status-success, .status-completed { background: #dcfce7; color: #166534; border-color: #bbf7d0; }
    .status-failed { background: #fee2e2; color: #991b1b; border-color: #fecaca; }
    .status-cancelled { background: #e5e7eb; color: #4b5563; border-color: #d1d5db; }
    .status-partial, .status-aborted { background: #ffedd5; color: #9a3412; border-color: #fed7aa; }
    .status-skipped { background: #fef9c3; color: #854d0e; border-color: #fde68a; }
    .status-notexecuted { background: #f3f4f6; color: #4b5563; border-color: #d1d5db; }
  `]
})
export class StatusBadgeComponent {
  @Input({ required: true }) status!: BoxRunStatus | TaskExecutionStatus | string;

  get cssClass(): string {
    return `status-${this.status.toLowerCase()}`;
  }
}
