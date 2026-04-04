import { Component, inject } from '@angular/core';
import { ErrorHandlerService } from '../services/error-handler.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  template: `
    @if (errorHandler.hasMessages()) {
      <div class="toast-container">
        @for (msg of errorHandler.messages(); track msg.id) {
          <div [class]="'toast toast-' + msg.type" role="alert">
            <span class="toast-text">{{ msg.text }}</span>
            <button type="button" class="toast-close" (click)="errorHandler.dismiss(msg.id)" aria-label="Dismiss">&times;</button>
          </div>
        }
      </div>
    }
  `,
  styles: [`
    .toast-container {
      position: fixed;
      top: 1rem;
      right: 1rem;
      z-index: 10000;
      display: flex;
      flex-direction: column;
      gap: .5rem;
      max-width: 420px;
    }
    .toast {
      display: flex;
      align-items: center;
      gap: .75rem;
      padding: .75rem 1rem;
      border-radius: var(--radius-1, 6px);
      font-size: .875rem;
      box-shadow: 0 4px 12px rgba(0,0,0,.15);
      animation: slideIn .25s ease-out;
    }
    .toast-error   { background: #fef2f2; color: #991b1b; border-left: 4px solid #ef4444; }
    .toast-success { background: #f0fdf4; color: #166534; border-left: 4px solid #22c55e; }
    .toast-info    { background: #eff6ff; color: #1e40af; border-left: 4px solid #3b82f6; }
    .toast-text    { flex: 1; }
    .toast-close   { background: none; border: none; font-size: 1.25rem; cursor: pointer; color: inherit; opacity: .6; line-height: 1; }
    .toast-close:hover { opacity: 1; }
    @keyframes slideIn { from { transform: translateX(100%); opacity: 0; } to { transform: translateX(0); opacity: 1; } }
  `]
})
export class ToastComponent {
  errorHandler = inject(ErrorHandlerService);
}
