import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LogsService } from '../../services/logs.service';
import { BoxesService } from '../../services/boxes.service';
import { ExecutionDto, BoxDto } from '../../models/models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="page-header">
      <h1>Dashboard</h1>
    </div>

    <div class="stats-row">
      <div class="stat-card">
        <span class="stat-value">{{ loadingTasks() ? '—' : tasks().length }}</span>
        <span class="stat-label">Total Boxes</span>
      </div>
      <div class="stat-card stat-warning">
        <span class="stat-value">{{ loadingLogs() ? '—' : runningCount() }}</span>
        <span class="stat-label">Running Now</span>
      </div>
      <div class="stat-card stat-success">
        <span class="stat-value">{{ loadingLogs() ? '—' : successToday() }}</span>
        <span class="stat-label">Succeeded Today</span>
      </div>
      <div class="stat-card stat-danger">
        <span class="stat-value">{{ loadingLogs() ? '—' : failedToday() }}</span>
        <span class="stat-label">Failed Today</span>
      </div>
    </div>

    <p class="section-title">Recent Executions</p>

    @if (loadingLogs()) {
      <div class="loading-state"><span class="spinner"></span> Loading logs…</div>
    } @else if (logsError()) {
      <div class="alert alert-danger">{{ logsError() }}</div>
    } @else if (logs().length === 0) {
      <p class="empty-state">No executions recorded yet.</p>
    } @else {
      <table class="data-table">
        <thead>
          <tr>
            <th>Task</th>
            <th>Status</th>
            <th>Started</th>
            <th>Duration</th>
          </tr>
        </thead>
        <tbody>
          @for (log of logs(); track log.executionId) {
            <tr>
              <td><a [routerLink]="['/logs']" class="table-link">{{ log.taskName }}</a></td>
              <td><span [class]="'badge badge-' + log.status.toLowerCase()">{{ log.status }}</span></td>
              <td>{{ log.startedAt | date:'short' }}</td>
              <td>{{ log.durationSeconds != null ? log.durationSeconds + 's' : '—' }}</td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: [`
    a.table-link { color: var(--primary); text-decoration: none; }
    a.table-link:hover { text-decoration: underline; }
  `]
})
export class DashboardComponent implements OnInit {
  private logsService   = inject(LogsService);
  private boxesService  = inject(BoxesService);

  tasks        = signal<BoxDto[]>([]);
  logs         = signal<ExecutionDto[]>([]);
  loadingTasks = signal(true);
  loadingLogs  = signal(true);
  logsError    = signal('');

  runningCount = signal(0);
  successToday = signal(0);
  failedToday  = signal(0);

  ngOnInit(): void {
    this.boxesService.getAll().subscribe({
      next: (t: BoxDto[]) => { this.tasks.set(t); this.loadingTasks.set(false); },
      error: () => this.loadingTasks.set(false)
    });

    this.logsService.getLatest(50).subscribe({
      next: data => {
        this.logs.set(data.slice(0, 10));
        const today = new Date().toDateString();
        const todayLogs = data.filter(l => new Date(l.startedAt).toDateString() === today);
        this.runningCount.set(data.filter(l => l.status === 'Running').length);
        this.successToday.set(todayLogs.filter(l => l.status === 'Success').length);
        this.failedToday.set(todayLogs.filter(l => l.status === 'Failed' || l.status === 'Timeout').length);
        this.loadingLogs.set(false);
      },
      error: () => {
        this.logsError.set('Unable to load execution history. Check API connection.');
        this.loadingLogs.set(false);
      }
    });
  }
}

