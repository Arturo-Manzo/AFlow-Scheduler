import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-confirm-modal',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (visible) {
      <div class="modal-overlay" role="dialog" aria-modal="true" (click)="cancelled.emit()">
        <div class="modal confirm-modal" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h3 style="margin:0;font-size:1rem">{{ title }}</h3>
            <button type="button" class="modal-close" (click)="cancelled.emit()" aria-label="Close">✕</button>
          </div>
          <div class="modal-body">
            <p class="confirm-message">{{ message }}</p>
          </div>
          <div class="modal-footer">
            <button class="btn" (click)="cancelled.emit()">Cancel</button>
            <button class="btn btn-danger" (click)="confirmed.emit()">{{ confirmLabel }}</button>
          </div>
        </div>
      </div>
    }
  `,
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
