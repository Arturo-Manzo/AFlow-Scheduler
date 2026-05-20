import { Component, DestroyRef, HostListener, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonDirective } from 'ui-design-system';
import { forkJoin } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ExecutionService } from '../../services/execution.service';
import { BoxesService } from '../../services/boxes.service';
import { ExecutionDto, BoxDto, RunningExecutionDto } from '../../models/models';
import { StatusBadgeComponent } from '../../components/status-badge/status-badge.component';
import { detectUserTimeZone, formatUtcWithZoneContext, getDateKeyInTimeZone } from '../../shared/timezone-utils';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, StatusBadgeComponent, ButtonDirective],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  private executionService = inject(ExecutionService);
  private boxesService = inject(BoxesService);
  private destroyRef = inject(DestroyRef);

  readonly userTimeZone = detectUserTimeZone();

  boxes = signal<BoxDto[]>([]);
  logs = signal<ExecutionDto[]>([]);
  running = signal<RunningExecutionDto[]>([]);
  selected = signal<ExecutionDto | null>(null);
  loadingBoxes = signal(true);
  loadingLogs = signal(true);
  logsError = signal('');

  runningCount = signal(0);
  successToday = signal(0);
  failedToday = signal(0);

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.selected.set(null);
  }

  ngOnInit(): void {
    this.boxesService.getAll().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (bs: BoxDto[]) => {
        this.boxes.set(bs);
        this.loadingBoxes.set(false);
      },
      error: () => this.loadingBoxes.set(false)
    });

    forkJoin({
      latest: this.executionService.getLatest(50),
      running: this.executionService.getRunning()
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: ({ latest, running }) => {
        this.logs.set(latest.slice(0, 10));
        this.running.set(running);
        const today = getDateKeyInTimeZone(new Date(), this.userTimeZone);
        const todayLogs = latest.filter(log => getDateKeyInTimeZone(log.startedAt, this.userTimeZone) === today);
        this.runningCount.set(running.length);
        this.successToday.set(todayLogs.filter(log => log.status === 'Success').length);
        this.failedToday.set(todayLogs.filter(log => log.status === 'Failed').length);
        this.loadingLogs.set(false);
      },
      error: () => {
        this.logsError.set('Unable to load execution history. Check API connection.');
        this.loadingLogs.set(false);
      }
    });
  }

  select(log: ExecutionDto): void {
    this.selected.set(this.selected()?.executionId === log.executionId ? null : log);
  }

  formatExecutionTime(value: string | undefined, boxTimeZoneId: string | undefined, variant: 'short' | 'medium'): string {
    return formatUtcWithZoneContext(
      value,
      this.userTimeZone,
      boxTimeZoneId,
      variant === 'short'
        ? { dateStyle: 'short', timeStyle: 'short' }
        : { dateStyle: 'medium', timeStyle: 'short' },
      { timeStyle: 'short' }
    );
  }

  displayRequestedBy(log: ExecutionDto): string {
    return log.requestedByUsername || (log.requestedByUserId ? 'Unknown user' : '');
  }

  activeBoxes(): number {
    return this.boxes().filter(box => box.enabled).length;
  }
}
