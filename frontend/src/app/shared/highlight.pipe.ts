import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'highlight', standalone: true, pure: true })
export class HighlightPipe implements PipeTransform {
  transform(value: string, query: string): string {
    const text = value ?? '';
    const escaped = this.escapeHtml(text);

    if (!query || query.trim().length < 2) {
      return escaped;
    }

    const escapedQuery = query.trim().replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    return escaped.replace(new RegExp(`(${escapedQuery})`, 'ig'), '<mark class="search-hit">$1</mark>');
  }

  private escapeHtml(value: string): string {
    return value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }
}
