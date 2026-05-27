import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonDirective } from 'ui-design-system';
import { TranslatePipe } from '../../shared/translate.pipe';

@Component({
  selector: 'app-error-modal',
  standalone: true,
  imports: [CommonModule, ButtonDirective, TranslatePipe],
  templateUrl: './error-modal.component.html',
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
    .modal-subtitle {
      margin-top: .2rem;
      color: var(--text-3);
      font-size: .82rem;
    }
  `]
})
export class ErrorModalComponent {
  @Input() visible = false;
  @Input() title = 'Execution Details';
  @Input() subtitle = '';
  @Input() loading = false;
  @Input() error = '';
  @Input() stackTrace = '';
  @Input() stdOut = '';
  @Output() close = new EventEmitter<void>();

  hasSummary(): boolean {
    return !!this.error.trim();
  }

  hasTechnicalDetails(): boolean {
    return !!this.stackTrace.trim() && this.stackTrace.trim() !== this.error.trim();
  }

  hasStdOut(): boolean {
    return !!this.stdOut.trim();
  }
}
