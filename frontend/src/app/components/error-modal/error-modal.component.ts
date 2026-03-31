import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-error-modal',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (visible) {
      <div class="modal-overlay" role="dialog" aria-modal="true" (click)="close.emit()">
        <div class="modal" (click)="$event.stopPropagation()" style="max-width:760px;width:95vw">
          <div class="modal-header">
            <h3 style="margin:0;font-size:1rem">Task Error Details</h3>
            <button type="button" class="modal-close" (click)="close.emit()" aria-label="Close">x</button>
          </div>
          <div class="modal-body">
            <p class="section-title" style="margin-top:0">Error Message</p>
            <pre class="error-block">{{ error || '(No error message)' }}</pre>

            <p class="section-title">Stack Trace</p>
            <pre class="stack-block">{{ stackTrace || '(No stack trace available)' }}</pre>
          </div>
          <div class="modal-footer">
            <button class="btn" (click)="close.emit()">Close</button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    pre {
      border: 1px solid var(--border);
      border-radius: var(--radius-1);
      padding: .75rem 1rem;
      font-family: var(--font-mono);
      font-size: .8rem;
      white-space: pre-wrap;
      word-break: break-word;
      max-height: 240px;
      overflow-y: auto;
      margin: 0;
    }
    .error-block {
      background: #fef2f2;
      color: #991b1b;
    }
    .stack-block {
      background: var(--bg-muted);
      color: var(--text-2);
      margin-top: .4rem;
    }
  `]
})
export class ErrorModalComponent {
  @Input() visible = false;
  @Input() error = '';
  @Input() stackTrace = '';
  @Output() close = new EventEmitter<void>();
}
