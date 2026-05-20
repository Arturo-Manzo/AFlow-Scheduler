import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonDirective } from 'ui-design-system';

@Component({
  selector: 'app-confirm-modal',
  standalone: true,
  imports: [CommonModule, ButtonDirective],
  templateUrl: './confirm-modal.component.html',
  styles: [`
    .confirm-modal { max-width: 440px; width: 95vw; }
    .confirm-message { margin: 0; color: var(--text-2); line-height: 1.55; }
    .modal-footer { display: flex; justify-content: flex-end; gap: .5rem; }
    .btn-danger { background: #dc2626; color: #fff; border-color: #dc2626; }
    .btn-danger:hover { background: #b91c1c; border-color: #b91c1c; }
  `]
})
export class ConfirmModalComponent {
  @Input() visible = false;
  @Input() title = 'Confirm';
  @Input() message = 'Are you sure?';
  @Input() confirmLabel = 'Confirm';
  @Output() confirmed = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();
}
