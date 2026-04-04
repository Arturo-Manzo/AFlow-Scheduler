import { Injectable, signal, computed } from '@angular/core';

export interface ToastMessage {
  id: number;
  text: string;
  type: 'error' | 'success' | 'info';
}

@Injectable({ providedIn: 'root' })
export class ErrorHandlerService {
  private nextId = 0;
  private _messages = signal<ToastMessage[]>([]);

  readonly messages = this._messages.asReadonly();
  readonly hasMessages = computed(() => this._messages().length > 0);

  showError(text: string): void {
    this.addMessage(text, 'error');
  }

  showSuccess(text: string): void {
    this.addMessage(text, 'success');
  }

  showInfo(text: string): void {
    this.addMessage(text, 'info');
  }

  dismiss(id: number): void {
    this._messages.update(msgs => msgs.filter(m => m.id !== id));
  }

  private addMessage(text: string, type: ToastMessage['type']): void {
    const id = ++this.nextId;
    this._messages.update(msgs => [...msgs, { id, text, type }]);
    setTimeout(() => this.dismiss(id), type === 'error' ? 8000 : 4000);
  }
}
